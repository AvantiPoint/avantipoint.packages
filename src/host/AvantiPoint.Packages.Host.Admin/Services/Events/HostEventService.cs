using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services.Events;

/// <summary>A webhook subscription defined under <c>Host:Webhooks</c>.</summary>
public class HostWebhookOptions
{
    public List<HostWebhookSubscription> Subscriptions { get; set; } = [];
}

public class HostWebhookSubscription
{
    public string Url { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 secret used to sign payloads (X-APFeed-Signature header).</summary>
    public string? Secret { get; set; }

    /// <summary>Event types to deliver (for example <c>package.published</c>). Empty = all events.</summary>
    public List<string> Events { get; set; } = [];
}

public sealed record HostEvent(string EventType, string Subject, string Actor, string? Detail, DateTimeOffset Timestamp);

/// <summary>
/// Records audit events to the identity database and queues them for webhook delivery.
/// </summary>
public interface IHostEventService
{
    Task RecordAsync(string eventType, string subject, string? detail = null, CancellationToken cancellationToken = default);
}

public sealed class HostEventService(
    IHostIdentityContext context,
    IHttpContextAccessor httpContextAccessor,
    HostEventChannel channel,
    TimeProvider timeProvider,
    ILogger<HostEventService> logger) : IHostEventService
{
    public async Task RecordAsync(string eventType, string subject, string? detail = null, CancellationToken cancellationToken = default)
    {
        var actor = httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
        var hostEvent = new HostEvent(eventType, subject, actor, detail, timeProvider.GetUtcNow());

        try
        {
            context.HostAuditEvents.Add(new HostAuditEvent
            {
                Timestamp = hostEvent.Timestamp,
                Actor = actor,
                EventType = eventType,
                Subject = subject,
                Detail = detail,
            });
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Auditing must never break the operation being audited.
            logger.LogError(ex, "Failed to persist audit event {EventType} for {Subject}", eventType, subject);
        }

        channel.Writer.TryWrite(hostEvent);
    }
}

/// <summary>Unbounded in-process queue between event producers and the webhook dispatcher.</summary>
public sealed class HostEventChannel
{
    private readonly Channel<HostEvent> _channel = Channel.CreateUnbounded<HostEvent>();

    public ChannelWriter<HostEvent> Writer => _channel.Writer;

    public ChannelReader<HostEvent> Reader => _channel.Reader;
}

/// <summary>
/// Delivers queued events to configured webhook subscriptions with HMAC-SHA256 signed
/// payloads and simple retry.
/// </summary>
public sealed class WebhookDispatcherService(
    HostEventChannel channel,
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<HostWebhookOptions> options,
    ILogger<WebhookDispatcherService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var hostEvent in channel.Reader.ReadAllAsync(stoppingToken))
        {
            foreach (var subscription in options.CurrentValue.Subscriptions)
            {
                if (string.IsNullOrWhiteSpace(subscription.Url) || !Matches(subscription, hostEvent.EventType))
                {
                    continue;
                }

                await DeliverAsync(subscription, hostEvent, stoppingToken);
            }
        }
    }

    public static bool Matches(HostWebhookSubscription subscription, string eventType) =>
        subscription.Events.Count == 0
        || subscription.Events.Contains(eventType, StringComparer.OrdinalIgnoreCase);

    public static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private async Task DeliverAsync(HostWebhookSubscription subscription, HostEvent hostEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            eventType = hostEvent.EventType,
            subject = hostEvent.Subject,
            actor = hostEvent.Actor,
            detail = hostEvent.Detail,
            timestamp = hostEvent.Timestamp,
        });

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-APFeed-Event", hostEvent.EventType);
                if (!string.IsNullOrEmpty(subscription.Secret))
                {
                    request.Headers.Add("X-APFeed-Signature", Sign(subscription.Secret, payload));
                }

                var client = httpClientFactory.CreateClient(nameof(WebhookDispatcherService));
                using var response = await client.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                logger.LogWarning(
                    "Webhook {Url} returned {StatusCode} for {EventType} (attempt {Attempt}/{Max})",
                    subscription.Url, (int)response.StatusCode, hostEvent.EventType, attempt, MaxAttempts);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Webhook delivery to {Url} failed for {EventType} (attempt {Attempt}/{Max})",
                    subscription.Url, hostEvent.EventType, attempt, MaxAttempts);
            }

            if (attempt < MaxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }
    }
}

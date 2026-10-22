using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services.Events;

public class HostWebhookOptions
{
    public List<HostWebhookSubscription> Subscriptions { get; set; } = [];
}

public class HostWebhookSubscription
{
    /// <summary>Stable identity across URL/secret changes. When omitted, the URL identifies the subscription.</summary>
    public string? Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Secret { get; set; }
    public List<string> Events { get; set; } = [];
    /// <summary>Only used when first registered. Zero includes existing audit history.</summary>
    public long StartAfterEventId { get; set; }
    public string StorageId => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Id ?? Url))).ToLowerInvariant();
}

public interface IHostEventService
{
    Task RecordAsync(string eventType, string subject, string? detail = null, CancellationToken cancellationToken = default);
}

public sealed class HostEventService(
    IHostIdentityContext context, IHttpContextAccessor httpContextAccessor, TimeProvider timeProvider) : IHostEventService
{
    public async Task RecordAsync(string eventType, string subject, string? detail = null, CancellationToken cancellationToken = default)
    {
        context.HostAuditEvents.Add(new HostAuditEvent
        {
            Timestamp = timeProvider.GetUtcNow(), Actor = httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system",
            EventType = eventType, Subject = subject, Detail = detail,
        });
        // The audit row is the durable source of delivery work. A failed write must not be
        // advertised as a successfully recorded event or sent only from process memory.
        await context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>At-least-once delivery with bounded batches, database leases, durable retries and dead letters.</summary>
public sealed class WebhookDispatcherService(
    IServiceScopeFactory scopes, IHttpClientFactory httpClientFactory, IOptionsMonitor<HostWebhookOptions> options,
    ILogger<WebhookDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var subscription in options.CurrentValue.Subscriptions)
            {
                try { await DispatchAsync(subscription, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception exception)
                {
                    // Do not log credentials, URL query parameters or receiver response bodies.
                    logger.LogError(exception, "Webhook subscription {SubscriptionId} will resume on the next pass", subscription.StorageId);
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task DispatchAsync(HostWebhookSubscription subscription, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<HostWebhookQueue>();
        await queue.EnqueueAsync(subscription, cancellationToken);
        for (var i = 0; i < 20; i++)
        {
            var delivery = await queue.ClaimAsync(subscription.StorageId, cancellationToken);
            if (delivery is null) break;
            var item = delivery.Event;
            var payload = JsonSerializer.Serialize(new
            {
                eventType = item.EventType, subject = item.Subject, actor = item.Actor,
                detail = item.Detail, timestamp = item.Timestamp,
            });
            string? error = null;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
                request.Headers.Add("X-APFeed-Event", item.EventType);
                // Receivers can de-duplicate a delivery retried after an ambiguous network failure.
                request.Headers.Add("X-APFeed-Delivery", $"{subscription.StorageId}.{item.Id}");
                if (!string.IsNullOrEmpty(subscription.Secret))
                    request.Headers.Add("X-APFeed-Signature", Sign(subscription.Secret, payload));
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                using var response = await httpClientFactory.CreateClient(nameof(WebhookDispatcherService))
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                if (!response.IsSuccessStatusCode) error = $"Receiver returned HTTP {(int)response.StatusCode}.";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or IOException)
            {
                error = "Receiver could not be reached or exceeded the delivery timeout.";
            }
            await queue.CompleteAttemptAsync(delivery, error, cancellationToken);
        }
    }

    public static bool Matches(HostWebhookSubscription subscription, string eventType) =>
        subscription.Events.Count == 0 || subscription.Events.Contains(eventType, StringComparer.OrdinalIgnoreCase);

    public static string Sign(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}

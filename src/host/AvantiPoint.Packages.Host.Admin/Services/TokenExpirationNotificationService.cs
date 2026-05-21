using System.Net.Mail;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Services.Email;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class TokenExpirationNotificationService(
    IServiceProvider serviceProvider,
    ILogger<TokenExpirationNotificationService> logger) : BackgroundService
{
    private readonly CronExpression _cronExpression = CronExpression.Parse("0 6 * * *", CronFormat.Standard);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var nextRun = _cronExpression.GetNextOccurrence(now, TimeZoneInfo.Utc);
            if (!nextRun.HasValue)
            {
                break;
            }

            var delay = nextRun.Value - now;
            try
            {
                await Task.Delay(delay, stoppingToken);
                if (!stoppingToken.IsCancellationRequested)
                {
                    await CheckTokenExpirationsAsync(stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Token expiration check failed");
            }
        }
    }

    private async Task CheckTokenExpirationsAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostIdentityContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTimeOffset.UtcNow;
        var tokens = await context.HostApiTokens
            .Include(t => t.User)
            .Where(t => !t.Revoked && !t.IsSystemToken)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            if (token.Expires <= now)
            {
                await TryNotifyAsync(context, emailService, token, EmailTemplateNames.TokenExpired, "token-expired", cancellationToken);
            }
            else if (token.Expires <= now.AddDays(3))
            {
                await TryNotifyAsync(context, emailService, token, EmailTemplateNames.TokenExpiring3Days, "token-expiring-3days", cancellationToken);
            }
            else if (token.Expires <= now.AddDays(7))
            {
                await TryNotifyAsync(context, emailService, token, EmailTemplateNames.TokenExpiring7Days, "token-expiring-7days", cancellationToken);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task TryNotifyAsync(
        IHostIdentityContext context,
        IEmailService emailService,
        Entities.HostApiToken token,
        string template,
        string notificationType,
        CancellationToken cancellationToken)
    {
        var alreadySent = await context.HostTokenNotifications.AnyAsync(
            n => n.HostApiTokenId == token.Id && n.NotificationType == notificationType,
            cancellationToken);

        if (alreadySent)
        {
            return;
        }

        var to = new MailAddress(token.User.Email, token.User.Name);
        await emailService.SendEmail(template, to, $"API Token Expiration - {token.Description}", new { token.Description, token.Expires });

        context.HostTokenNotifications.Add(new Entities.HostTokenNotification
        {
            HostApiTokenId = token.Id,
            NotificationType = notificationType,
            SentAt = DateTimeOffset.UtcNow,
        });
    }
}

using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class AuditLogModel(IHostAuditLogService auditLog, IHostIdentityContext context, IOptionsMonitor<HostWebhookOptions> webhooks) : PageModel
{
    private const int PageSize = 200;

    public IList<HostAuditEvent> Events { get; private set; } = [];

    public string? EventTypeFilter { get; private set; }
    public IList<HostWebhookDelivery> FailedDeliveries { get; private set; } = [];
    public int PendingDeliveries { get; private set; }
    [TempData] public string? StatusMessage { get; set; }

    public string SubscriptionName(string id)
    {
        var subscription = webhooks.CurrentValue.Subscriptions.FirstOrDefault(s => s.StorageId == id);
        return subscription is null ? "Unconfigured subscription" : subscription.Id ?? new Uri(subscription.Url).Host;
    }

    public async Task OnGetAsync(string? eventType)
    {
        EventTypeFilter = eventType;
        Events = await auditLog.GetRecentAsync(eventType, PageSize, HttpContext.RequestAborted);
        PendingDeliveries = await context.HostWebhookDeliveries.CountAsync(d => d.Status == HostWebhookDeliveryStatus.Pending, HttpContext.RequestAborted);
        FailedDeliveries = await context.HostWebhookDeliveries.AsNoTracking().Include(d => d.Event)
            .Where(d => d.Status == HostWebhookDeliveryStatus.Failed).OrderByDescending(d => d.Id).Take(100).ToListAsync(HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostRetryWebhookAsync(long id)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(HttpContext.RequestAborted);
        var delivery = await context.HostWebhookDeliveries.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, HttpContext.RequestAborted);
        if (delivery is null) return NotFound();
        if (!webhooks.CurrentValue.Subscriptions.Any(s => s.StorageId == delivery.SubscriptionId))
        {
            StatusMessage = "Restore this webhook subscription in configuration before retrying.";
            return RedirectToPage();
        }
        var changed = await context.HostWebhookDeliveries.Where(d => d.Id == id && d.Status == HostWebhookDeliveryStatus.Failed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.Status, HostWebhookDeliveryStatus.Pending)
                .SetProperty(d => d.Attempts, 0).SetProperty(d => d.NextAttemptUnixMilliseconds, 0)
                .SetProperty(d => d.LeaseOwner, (string?)null).SetProperty(d => d.LeaseExpiresUnixMilliseconds, 0), HttpContext.RequestAborted);
        if (changed == 1)
        {
            context.Audit(User.Identity?.Name, "webhook.retried", id.ToString());
            await context.SaveChangesAsync(HttpContext.RequestAborted);
        }
        await transaction.CommitAsync(HttpContext.RequestAborted);
        StatusMessage = changed == 1 ? "Webhook queued for another delivery attempt." : "This delivery is no longer failed.";
        return RedirectToPage();
    }
}

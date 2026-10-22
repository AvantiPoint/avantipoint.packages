using System.Data;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Services.Events;

public sealed class HostWebhookQueue(IHostIdentityContext context, TimeProvider clock)
{
    public const int MaxAttempts = 5;

    public async Task EnqueueAsync(HostWebhookSubscription subscription, CancellationToken cancellationToken = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var id = subscription.StorageId;
        var checkpoint = await context.HostWebhookCheckpoints.SingleOrDefaultAsync(c => c.SubscriptionId == id, cancellationToken);
        if (checkpoint is null)
        {
            checkpoint = new HostWebhookCheckpoint { SubscriptionId = id, LastQueuedEventId = subscription.StartAfterEventId };
            context.HostWebhookCheckpoints.Add(checkpoint);
        }
        var events = await context.HostAuditEvents.AsNoTracking().Where(e => e.Id > checkpoint.LastQueuedEventId)
            .OrderBy(e => e.Id).Take(100).Select(e => new { e.Id, e.EventType }).ToListAsync(cancellationToken);
        foreach (var item in events)
            if (WebhookDispatcherService.Matches(subscription, item.EventType))
                context.HostWebhookDeliveries.Add(new HostWebhookDelivery { SubscriptionId = id, EventId = item.Id });
        if (events.Count > 0) checkpoint.LastQueuedEventId = events[^1].Id;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HostWebhookDelivery?> ClaimAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().ToUnixTimeMilliseconds();
        var candidates = context.HostWebhookDeliveries.Where(d => d.SubscriptionId == subscriptionId
            && d.Status == HostWebhookDeliveryStatus.Pending && d.NextAttemptUnixMilliseconds <= now
            && d.LeaseExpiresUnixMilliseconds <= now);
        var ids = await candidates.OrderBy(d => d.EventId).Take(20).Select(d => d.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            var owner = Guid.NewGuid().ToString("N");
            var claimed = await candidates.Where(d => d.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(d => d.LeaseOwner, owner).SetProperty(d => d.LeaseExpiresUnixMilliseconds, now + 60_000), cancellationToken);
            if (claimed == 1)
                return await context.HostWebhookDeliveries.AsNoTracking().Include(d => d.Event).SingleAsync(d => d.Id == id, cancellationToken);
        }
        return null;
    }

    public async Task CompleteAttemptAsync(HostWebhookDelivery delivery, string? error, CancellationToken cancellationToken = default)
    {
        var attempts = delivery.Attempts + 1;
        var status = error is null ? HostWebhookDeliveryStatus.Delivered
            : attempts >= MaxAttempts ? HostWebhookDeliveryStatus.Failed : HostWebhookDeliveryStatus.Pending;
        var next = clock.GetUtcNow().AddSeconds(Math.Min(300, Math.Pow(2, attempts))).ToUnixTimeMilliseconds();
        await context.HostWebhookDeliveries.Where(d => d.Id == delivery.Id && d.LeaseOwner == delivery.LeaseOwner)
            .ExecuteUpdateAsync(setters => setters.SetProperty(d => d.Status, status)
                .SetProperty(d => d.Attempts, attempts).SetProperty(d => d.LastError, error)
                .SetProperty(d => d.NextAttemptUnixMilliseconds, next)
                .SetProperty(d => d.LeaseOwner, (string?)null).SetProperty(d => d.LeaseExpiresUnixMilliseconds, 0), cancellationToken);
    }
}

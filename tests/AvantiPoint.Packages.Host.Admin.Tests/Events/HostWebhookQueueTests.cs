using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Tests.Events;

public sealed class HostWebhookQueueTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AdjustableClock _clock = new();
    private readonly HostWebhookSubscription _subscription = new() { Id = "build-events", Url = "https://receiver.example.test", Events = ["package.published"] };

    public HostWebhookQueueTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task Restart_ResumesPersistedEvents_WithoutDuplicates()
    {
        await using (var context = CreateContext())
        {
            context.HostAuditEvents.Add(Event("package.published"));
            context.HostAuditEvents.Add(Event("settings.updated"));
            await context.SaveChangesAsync(Ct);
        }
        // Producer and dispatcher do not share a process-local queue or DbContext.
        await using (var restarted = CreateContext())
        {
            var queue = new HostWebhookQueue(restarted, _clock);
            await queue.EnqueueAsync(_subscription, Ct);
            await queue.EnqueueAsync(_subscription, Ct);
            Assert.Single(await restarted.HostWebhookDeliveries.ToListAsync(Ct));
            Assert.Equal(2, (await restarted.HostWebhookCheckpoints.SingleAsync(Ct)).LastQueuedEventId);
        }
        await using var nextRestart = CreateContext();
        var nextQueue = new HostWebhookQueue(nextRestart, _clock);
        var delivery = Assert.IsType<HostWebhookDelivery>(await nextQueue.ClaimAsync(_subscription.StorageId, Ct));
        await nextQueue.CompleteAttemptAsync(delivery, error: null, Ct);
        Assert.Null(await nextQueue.ClaimAsync(_subscription.StorageId, Ct));
        Assert.Equal(HostWebhookDeliveryStatus.Delivered, (await nextRestart.HostWebhookDeliveries.SingleAsync(Ct)).Status);
    }

    [Fact]
    public async Task Lease_PreventsConcurrentDelivery_AndCanRecoverAfterCrash()
    {
        await using var first = CreateContext();
        await SeedOneAsync(first);
        var queue = new HostWebhookQueue(first, _clock);
        await queue.EnqueueAsync(_subscription, Ct);
        var initial = Assert.IsType<HostWebhookDelivery>(await queue.ClaimAsync(_subscription.StorageId, Ct));
        await using var other = CreateContext();
        var competitor = new HostWebhookQueue(other, _clock);
        Assert.Null(await competitor.ClaimAsync(_subscription.StorageId, Ct));
        _clock.Advance(TimeSpan.FromSeconds(61));
        var recovered = Assert.IsType<HostWebhookDelivery>(await competitor.ClaimAsync(_subscription.StorageId, Ct));
        Assert.Equal(initial.Id, recovered.Id);
        Assert.NotEqual(initial.LeaseOwner, recovered.LeaseOwner);
        await queue.CompleteAttemptAsync(initial, error: null, Ct);
        Assert.Equal(HostWebhookDeliveryStatus.Pending, (await other.HostWebhookDeliveries.AsNoTracking().SingleAsync(Ct)).Status);
        await competitor.CompleteAttemptAsync(recovered, error: null, Ct);
        Assert.Equal(HostWebhookDeliveryStatus.Delivered, (await other.HostWebhookDeliveries.AsNoTracking().SingleAsync(Ct)).Status);
    }

    [Fact]
    public async Task Failures_AreBackedOff_ThenRetainedAsDeadLetters()
    {
        await using var context = CreateContext();
        await SeedOneAsync(context);
        var queue = new HostWebhookQueue(context, _clock);
        await queue.EnqueueAsync(_subscription, Ct);
        for (var attempt = 1; attempt <= HostWebhookQueue.MaxAttempts; attempt++)
        {
            var delivery = Assert.IsType<HostWebhookDelivery>(await queue.ClaimAsync(_subscription.StorageId, Ct));
            await queue.CompleteAttemptAsync(delivery, "Receiver returned HTTP 503.", Ct);
            Assert.Null(await queue.ClaimAsync(_subscription.StorageId, Ct));
            _clock.Advance(TimeSpan.FromMinutes(6));
        }
        Assert.Null(await queue.ClaimAsync(_subscription.StorageId, Ct));
        var failed = await context.HostWebhookDeliveries.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(HostWebhookDeliveryStatus.Failed, failed.Status);
        Assert.Equal(5, failed.Attempts);
        Assert.NotNull(failed.LastError);
    }

    [Fact]
    public async Task Backfill_IsBounded_AndStartCursorIsRespected()
    {
        await using var context = CreateContext();
        context.HostAuditEvents.AddRange(Enumerable.Range(0, 220).Select(_ => Event("package.published")));
        await context.SaveChangesAsync(Ct);
        _subscription.StartAfterEventId = 10;
        var queue = new HostWebhookQueue(context, _clock);
        await queue.EnqueueAsync(_subscription, Ct);
        Assert.Equal(100, await context.HostWebhookDeliveries.CountAsync(Ct));
        await queue.EnqueueAsync(_subscription, Ct);
        Assert.Equal(200, await context.HostWebhookDeliveries.CountAsync(Ct));
        await queue.EnqueueAsync(_subscription, Ct);
        Assert.Equal(210, await context.HostWebhookDeliveries.CountAsync(Ct));
    }

    private async Task SeedOneAsync(HostSqliteContext context)
    {
        context.HostAuditEvents.Add(Event("package.published"));
        await context.SaveChangesAsync(Ct);
    }
    private static HostAuditEvent Event(string type) => new() { EventType = type, Actor = "test", Subject = "demo-package", Timestamp = DateTimeOffset.UtcNow };
    private HostSqliteContext CreateContext() => new(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(_connection).Options);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
    private sealed class AdjustableClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}

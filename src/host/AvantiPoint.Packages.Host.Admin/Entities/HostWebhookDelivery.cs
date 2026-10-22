namespace AvantiPoint.Packages.Host.Admin.Entities;

public enum HostWebhookDeliveryStatus { Pending, Delivered, Failed }

public sealed class HostWebhookDelivery
{
    public long Id { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public long EventId { get; set; }
    public HostAuditEvent Event { get; set; } = null!;
    public HostWebhookDeliveryStatus Status { get; set; }
    public int Attempts { get; set; }
    public long NextAttemptUnixMilliseconds { get; set; }
    public string? LeaseOwner { get; set; }
    public long LeaseExpiresUnixMilliseconds { get; set; }
    public string? LastError { get; set; }
}

public sealed class HostWebhookCheckpoint
{
    public string SubscriptionId { get; set; } = string.Empty;
    public long LastQueuedEventId { get; set; }
}

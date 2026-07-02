namespace AvantiPoint.Packages.Host.Admin.Entities;

/// <summary>
/// A persisted record of a security- or lifecycle-relevant action (package published,
/// group promoted, publish target changed, and so on).
/// </summary>
public class HostAuditEvent
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The authenticated user (or system component) that performed the action.</summary>
    public string Actor { get; set; } = string.Empty;

    /// <summary>Stable event type identifier, for example <c>package.published</c> or <c>group.promoted</c>.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The object acted upon (package id, group name, target name, ...).</summary>
    public string Subject { get; set; } = string.Empty;

    public string? Detail { get; set; }
}

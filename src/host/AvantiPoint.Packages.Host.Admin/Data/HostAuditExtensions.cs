using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Data;

public static class HostAuditExtensions
{
    /// <summary>Stages an audit row in the same save/transaction as an identity-management change.</summary>
    public static void Audit(this IHostIdentityContext context, string? actor, string eventType, string subject, string? detail = null) =>
        context.HostAuditEvents.Add(new HostAuditEvent
        {
            Actor = actor ?? "system", EventType = eventType, Subject = subject, Detail = detail, Timestamp = DateTimeOffset.UtcNow,
        });
}

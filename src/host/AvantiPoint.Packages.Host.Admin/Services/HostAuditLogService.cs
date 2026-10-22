using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Admin.Services;

public interface IHostAuditLogService
{
    Task<IList<HostAuditEvent>> GetRecentAsync(
        string? eventType,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class HostAuditLogService(IHostIdentityContext context) : IHostAuditLogService
{
    public async Task<IList<HostAuditEvent>> GetRecentAsync(
        string? eventType,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        var query = context.HostAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        // Audit event IDs are generated in insertion order. Use that sequence for
        // the newest-recorded view because SQLite cannot translate ORDER BY for
        // DateTimeOffset values.
        return await query
            .OrderByDescending(e => e.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class AuditLogModel(IHostIdentityContext context) : PageModel
{
    private const int PageSize = 200;

    public IList<HostAuditEvent> Events { get; private set; } = [];

    public string? EventTypeFilter { get; private set; }

    public async Task OnGetAsync(string? eventType)
    {
        EventTypeFilter = eventType;
        var query = context.HostAuditEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(e => e.EventType == eventType);
        }

        Events = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(PageSize)
            .ToListAsync();
    }
}

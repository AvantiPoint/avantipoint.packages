using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class UsersModel(IHostIdentityContext db) : PageModel
{
    public IList<HostUser> Users { get; private set; } = [];

    public async Task OnGetAsync() =>
        Users = await db.HostUsers.OrderBy(u => u.Email).ToListAsync();

    public async Task<IActionResult> OnPostTogglePublishAsync(string email)
    {
        var user = await db.HostUsers.FirstAsync(u => u.Email == email);
        user.CanPublish = !user.CanPublish;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveAsync(string email)
    {
        var user = await db.HostUsers.FirstAsync(u => u.Email == email);
        user.ApprovalStatus = HostUserApprovalStatus.Approved;
        user.CanConsume = true;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync(string email)
    {
        var user = await db.HostUsers.FirstAsync(u => u.Email == email);
        user.IsRevoked = true;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}

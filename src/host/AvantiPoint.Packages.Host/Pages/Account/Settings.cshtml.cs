using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class SettingsModel(IHostIdentityContext db) : PageModel
{
    [BindProperty]
    public bool RequireNewUserApproval { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var settings = await db.HostAccessSettings.SingleAsync(s => s.Id == 1);
        RequireNewUserApproval = settings.RequireNewUserApproval;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var settings = await db.HostAccessSettings.SingleAsync(s => s.Id == 1);
        settings.RequireNewUserApproval = RequireNewUserApproval;
        db.Audit(User.Identity?.Name, "settings.updated", "access", $"requireNewUserApproval={RequireNewUserApproval}");
        await db.SaveChangesAsync();
        StatusMessage = "Access settings saved.";
        return RedirectToPage();
    }
}

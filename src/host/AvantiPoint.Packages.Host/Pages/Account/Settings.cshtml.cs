using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AvantiPoint.Packages.Host.Configuration;
using System.Security.Claims;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class SettingsModel(IHostIdentityContext db, HostFeedConfigurationStore feedConfiguration) : PageModel
{
    [BindProperty]
    public bool RequireNewUserApproval { get; set; }

    [BindProperty]
    public HostFeedConfiguration Feed { get; set; } = new();

    public bool RestartRequired { get; private set; }

    [TempData]
    public string? SavedMessage { get; set; }

    private Task<bool> IsCurrentAdministratorAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("preferred_username");
        return db.HostUsers.AsNoTracking().AnyAsync(user => user.Email == email && user.IsAdmin
            && !user.IsRevoked && user.ApprovalStatus == HostUserApprovalStatus.Approved, HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await IsCurrentAdministratorAsync()) return StatusCode(403);
        var settings = await db.HostAccessSettings.FirstAsync();
        RequireNewUserApproval = settings.RequireNewUserApproval;
        Feed = feedConfiguration.ReadSaved();
        RestartRequired = Feed != feedConfiguration.Active;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await IsCurrentAdministratorAsync()) return StatusCode(403);
        RestartRequired = feedConfiguration.ReadSaved() != feedConfiguration.Active;
        if (!ModelState.IsValid) return Page();
        try
        {
            await feedConfiguration.SaveAsync(Feed, HttpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "Could not save feed configuration. Check the configured configuration location and its file permissions.");
            return Page();
        }
        var settings = await db.HostAccessSettings.FirstAsync();
        settings.RequireNewUserApproval = RequireNewUserApproval;
        await db.SaveChangesAsync();
        SavedMessage = "Saved. Restart the host to apply feed configuration changes. New-user approval applies immediately.";
        return RedirectToPage();
    }
}

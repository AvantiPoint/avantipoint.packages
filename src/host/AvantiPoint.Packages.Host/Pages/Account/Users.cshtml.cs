using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Authorization;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Admin)]
public class UsersModel(IHostIdentityContext db, IOptions<HostAuthenticationOptions> authenticationOptions,
    HostUserManagementService userManagement) : PageModel
{
    public IList<HostUser> Users { get; private set; } = [];
    public bool AdminRolesManagedByEntra => HostRoleAssignmentPolicy.AdminIsManagedByEntra(authenticationOptions.Value);
    public bool PublishingManagedByEntra => HostRoleAssignmentPolicy.PublishingIsManagedByEntra(authenticationOptions.Value);
    public bool DedicatedPublisherGroupConfigured => HostRoleAssignmentPolicy.PublisherGroupIsConfigured(authenticationOptions.Value);
    public string CurrentUserEmail => User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    [TempData] public string? StatusMessage { get; set; }
    [TempData] public bool StatusIsError { get; set; }

    public async Task OnGetAsync() => Users = await db.HostUsers.AsNoTracking().OrderBy(u => u.Email).ToListAsync(HttpContext.RequestAborted);
    public Task<IActionResult> OnPostTogglePublishAsync(string email) => ChangeAsync(email, HostUserChange.TogglePublish);
    public Task<IActionResult> OnPostToggleAdminAsync(string email) => ChangeAsync(email, HostUserChange.ToggleAdmin);
    public Task<IActionResult> OnPostApproveAsync(string email) => ChangeAsync(email, HostUserChange.Approve);
    public Task<IActionResult> OnPostRevokeAsync(string email) => ChangeAsync(email, HostUserChange.Revoke);
    public Task<IActionResult> OnPostRestoreAsync(string email) => ChangeAsync(email, HostUserChange.Restore);

    private async Task<IActionResult> ChangeAsync(string email, HostUserChange change)
    {
        var result = await userManagement.ChangeAsync(CurrentUserEmail, email, change, HttpContext.RequestAborted);
        StatusMessage = result.Message;
        StatusIsError = !result.Succeeded;
        return RedirectToPage();
    }
}

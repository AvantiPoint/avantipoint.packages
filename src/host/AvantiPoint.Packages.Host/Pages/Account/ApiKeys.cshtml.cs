using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Email;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Claims;

namespace AvantiPoint.Packages.Host.Pages.Account;

[Authorize(Roles = FeedRoles.Consumer)]
public class ApiKeysModel(
    IHostIdentityContext db,
    IHostTokenHasher tokenHasher,
    IEmailService emailService) : PageModel
{
    public IList<HostApiToken> Tokens { get; private set; } = [];

    [BindProperty]
    public string? NewDescription { get; set; }

    [BindProperty]
    public FeedTokenScope NewScopes { get; set; } = FeedTokenScope.ReadWrite;

    public string? LastCreatedToken { get; set; }

    public async Task OnGetAsync()
    {
        await LoadTokensAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!;
        var user = await db.HostUsers.FirstAsync(u => u.Email == email);
        if (!user.CanPublish && NewScopes.HasFlag(FeedTokenScope.Write))
        {
            ModelState.AddModelError(string.Empty, "You cannot create write tokens.");
            await LoadTokensAsync();
            return Page();
        }

        var (plaintext, prefix, hash) = tokenHasher.GenerateToken();
        db.HostApiTokens.Add(new HostApiToken
        {
            Description = NewDescription ?? "API Token",
            UserEmail = email,
            TokenPrefix = prefix,
            TokenHash = hash,
            Created = DateTimeOffset.UtcNow,
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            Scopes = NewScopes,
        });
        await db.SaveChangesAsync();
        LastCreatedToken = plaintext;
        await LoadTokensAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(int id)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!;
        var token = await db.HostApiTokens.FirstAsync(t => t.Id == id && t.UserEmail == email && !t.IsSystemToken);
        token.Revoked = true;
        await db.SaveChangesAsync();
        return RedirectToPage();
    }

    private async Task LoadTokensAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email)!;
        Tokens = await db.HostApiTokens
            .Where(t => t.UserEmail == email && !t.IsSystemToken)
            .OrderByDescending(t => t.Created)
            .ToListAsync();
    }
}

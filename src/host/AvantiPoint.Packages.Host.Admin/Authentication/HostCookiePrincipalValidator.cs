using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Authentication;

/// <summary>
/// Keeps an authenticated website session aligned with the user's current database roles.
/// This makes local role changes and account revocation effective on the next request rather
/// than waiting for the authentication cookie to expire.
/// </summary>
public static class HostCookiePrincipalValidator
{
    public const string SessionVersionClaim = "ap:session-version";
    private static readonly HashSet<string> HostRoles =
    [
        FeedRoles.Admin,
        FeedRoles.Publisher,
        FeedRoles.Consumer,
    ];

    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var email = context.Principal?.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            await RejectAsync(context);
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<IHostIdentityContext>();
        var user = await db.HostUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Email == email, context.HttpContext.RequestAborted);

        if (user is null
            || user.IsRevoked
            || user.ApprovalStatus != HostUserApprovalStatus.Approved)
        {
            await RejectAsync(context);
            return;
        }

        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            await RejectAsync(context);
            return;
        }

        var versionClaim = context.Principal.FindFirstValue(SessionVersionClaim);
        var sessionVersion = versionClaim is null ? 0
            : int.TryParse(versionClaim, out var parsedVersion) ? parsedVersion : -1;
        if (sessionVersion != user.SessionVersion)
        {
            await RejectAsync(context);
            return;
        }

        var expectedRoles = GetExpectedRoles(user);
        var currentRoleClaims = context.Principal.Identities
            .SelectMany(candidate => candidate.FindAll(ClaimTypes.Role))
            .Where(claim => HostRoles.Contains(claim.Value))
            .ToList();
        var currentRoles = currentRoleClaims
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        if (currentRoleClaims.Count == expectedRoles.Count
            && currentRoles.SetEquals(expectedRoles))
        {
            return;
        }

        foreach (var claimsIdentity in context.Principal.Identities)
        {
            foreach (var claim in claimsIdentity.FindAll(ClaimTypes.Role)
                         .Where(claim => HostRoles.Contains(claim.Value))
                         .ToList())
            {
                claimsIdentity.RemoveClaim(claim);
            }
        }

        foreach (var role in expectedRoles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        context.ShouldRenew = true;
    }

    private static HashSet<string> GetExpectedRoles(HostUser user)
    {
        var roles = new HashSet<string>(StringComparer.Ordinal);
        if (user.IsAdmin)
        {
            roles.Add(FeedRoles.Admin);
        }

        if (user.CanPublish)
        {
            roles.Add(FeedRoles.Publisher);
        }

        if (user.CanConsume)
        {
            roles.Add(FeedRoles.Consumer);
        }

        return roles;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}

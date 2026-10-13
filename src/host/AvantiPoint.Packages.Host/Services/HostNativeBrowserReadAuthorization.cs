using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Host.Services;

public sealed class HostNativeBrowserReadAuthorization(IHostIdentityContext identity) : INativeBrowserReadAuthorization
{
    public Task<bool> CanReadAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var email = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("preferred_username");
        if (email is null) return Task.FromResult(false);
        return identity.HostUsers.AsNoTracking().AnyAsync(account => account.Email == email
            && account.CanConsume && !account.IsRevoked
            && account.ApprovalStatus == HostUserApprovalStatus.Approved, cancellationToken);
    }
}

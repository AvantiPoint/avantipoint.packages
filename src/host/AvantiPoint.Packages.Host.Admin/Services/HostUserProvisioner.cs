using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class HostUserProvisioner(
    IHostIdentityContext context,
    IHostTokenHasher tokenHasher,
    IOptions<HostAccessOptions> accessOptions,
    IOptions<HostAuthenticationOptions> authOptions) : IHostUserProvisioner
{
    public async Task<HostUser> ProvisionUserAsync(
        ClaimsPrincipal principal,
        HostExternalAuthProvider provider,
        CancellationToken cancellationToken = default)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("preferred_username")
            ?? throw new InvalidOperationException("Email claim is required.");

        var name = principal.FindFirstValue(ClaimTypes.Name) ?? email;
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? email;

        var user = await context.HostUsers
            .Include(u => u.Tokens)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        var hasAdmin = await context.HostUsers.AnyAsync(u => u.IsAdmin && !u.IsRevoked, cancellationToken);
        var settings = await context.HostAccessSettings.FirstOrDefaultAsync(cancellationToken)
            ?? new HostAccessSettings { Id = 1, RequireNewUserApproval = accessOptions.Value.RequireNewUserApproval };

        if (user is null)
        {
            user = new HostUser
            {
                Email = email,
                Name = name,
                ExternalProvider = provider,
                ExternalSubjectId = subject,
                CreatedAt = DateTimeOffset.UtcNow,
                IsRevoked = false,
            };

            if (!hasAdmin)
            {
                user.IsAdmin = true;
                user.CanPublish = true;
                user.CanConsume = true;
                user.ApprovalStatus = HostUserApprovalStatus.Approved;
            }
            else if (settings.RequireNewUserApproval)
            {
                user.ApprovalStatus = HostUserApprovalStatus.Pending;
                user.CanConsume = false;
                user.CanPublish = false;
            }
            else
            {
                user.ApprovalStatus = HostUserApprovalStatus.Approved;
                ApplyEntraRoles(user, principal);
            }

            context.HostUsers.Add(user);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (user.IsRevoked || user.ApprovalStatus == HostUserApprovalStatus.Denied)
            {
                throw new UnauthorizedAccessException("User access has been revoked.");
            }

            user.LastLoginAt = DateTimeOffset.UtcNow;
            user.Name = name;
            if (provider == HostExternalAuthProvider.MicrosoftEntra)
            {
                ApplyEntraRoles(user, principal);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        await EnsureSystemTokenAsync(user, cancellationToken);
        return user;
    }

    private void ApplyEntraRoles(HostUser user, ClaimsPrincipal principal)
    {
        var entra = authOptions.Value.MicrosoftEntra;
        if (entra.AdminRoleGroupIds.Count == 0 &&
            entra.PublisherRoleGroupIds.Count == 0 &&
            entra.ConsumerRoleGroupIds.Count == 0)
        {
            user.CanConsume = true;
            return;
        }

        var groups = principal.FindAll("groups").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        user.IsAdmin = entra.AdminRoleGroupIds.Any(groups.Contains);
        user.CanPublish = user.IsAdmin || entra.PublisherRoleGroupIds.Any(groups.Contains);
        user.CanConsume = user.CanPublish || entra.ConsumerRoleGroupIds.Any(groups.Contains) || entra.ConsumerRoleGroupIds.Count == 0;
    }

    private async Task EnsureSystemTokenAsync(HostUser user, CancellationToken cancellationToken)
    {
        if (user.Tokens.Any(t => t.IsSystemToken && !t.Revoked))
        {
            return;
        }

        var (plaintext, prefix, hash) = tokenHasher.GenerateToken();
        user.Tokens.Add(new HostApiToken
        {
            TokenPrefix = prefix,
            TokenHash = hash,
            Description = "System Token",
            UserEmail = user.Email,
            Created = DateTimeOffset.UtcNow,
            Expires = DateTimeOffset.UtcNow.AddHours(24),
            IsSystemToken = true,
            Scopes = FeedTokenScope.ReadWrite,
        });

        await context.SaveChangesAsync(cancellationToken);
        _ = plaintext;
    }
}

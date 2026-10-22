using System.Data;
using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authorization;
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
        // The first-user check and insert must be one serializable operation across
        // host instances. Competing sign-ins cannot both become the initial admin.
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var email = principal.FindFirstValue(ClaimTypes.Email)
            ?? principal.FindFirstValue("preferred_username")
            ?? throw new InvalidOperationException("Email claim is required.");

        var name = principal.FindFirstValue(ClaimTypes.Name) ?? email;
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? email;

        var user = await context.HostUsers
            .Include(u => u.Tokens)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        var hasUsers = await context.HostUsers.AnyAsync(cancellationToken);
        var settings = await context.HostAccessSettings
            .SingleOrDefaultAsync(s => s.Id == 1, cancellationToken)
            ?? new HostAccessSettings { Id = 1, RequireNewUserApproval = accessOptions.Value.RequireNewUserApproval };
        var roleOptions = authOptions.Value;
        var adminManagedByEntra = provider == HostExternalAuthProvider.MicrosoftAccount
            && HostRoleAssignmentPolicy.AdminIsManagedByEntra(roleOptions);
        var publishingManagedByEntra = provider == HostExternalAuthProvider.MicrosoftAccount
            && HostRoleAssignmentPolicy.PublishingIsManagedByEntra(roleOptions);

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

            if (provider == HostExternalAuthProvider.MicrosoftAccount)
            {
                HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, principal, roleOptions);
            }

            if (!hasUsers && !adminManagedByEntra)
            {
                user.IsAdmin = true;
                if (!publishingManagedByEntra)
                {
                    user.CanPublish = true;
                }
                user.CanConsume = true;
                user.ApprovalStatus = HostUserApprovalStatus.Approved;
            }
            else if (user.IsAdmin && adminManagedByEntra)
            {
                // An Entra-managed administrator must be able to establish the first usable
                // administrator account even when new-user approval is otherwise required.
                user.CanConsume = true;
                user.ApprovalStatus = HostUserApprovalStatus.Approved;
            }
            else if (settings.RequireNewUserApproval)
            {
                user.ApprovalStatus = HostUserApprovalStatus.Pending;
                user.CanConsume = false;
            }
            else
            {
                user.ApprovalStatus = HostUserApprovalStatus.Approved;
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
            if (provider == HostExternalAuthProvider.MicrosoftAccount)
            {
                HostRoleAssignmentPolicy.ApplyMicrosoftRoles(user, principal, roleOptions);
                if (user.IsAdmin && adminManagedByEntra
                    && user.ApprovalStatus == HostUserApprovalStatus.Pending)
                {
                    user.ApprovalStatus = HostUserApprovalStatus.Approved;
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        await EnsureSystemTokenAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    private async Task EnsureSystemTokenAsync(HostUser user, CancellationToken cancellationToken)
    {
        if (user.Tokens.Any(t => t.IsSystemToken && t.IsValid()))
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

using System.Data;
using AvantiPoint.Packages.Host.Admin.Authorization;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Services;

public enum HostUserChange { Approve, Revoke, Restore, ToggleAdmin, TogglePublish }
public sealed record HostUserChangeResult(bool Succeeded, string Message);

/// <summary>Enforces role authority and records account changes atomically.</summary>
public sealed class HostUserManagementService(IHostIdentityContext db, IOptions<HostAuthenticationOptions> authentication)
{
    public async Task<HostUserChangeResult> ChangeAsync(string actorEmail, string email, HostUserChange change,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var actor = await db.HostUsers.SingleOrDefaultAsync(u => u.Email == actorEmail, cancellationToken);
        if (actor is not { IsAdmin: true, IsRevoked: false, ApprovalStatus: HostUserApprovalStatus.Approved })
            return Fail("Administrator access is required.");
        var user = await db.HostUsers.Include(u => u.Tokens).SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null) return Fail("The selected account no longer exists.");
        var options = authentication.Value;
        var adminManaged = HostRoleAssignmentPolicy.AdminIsManagedByEntra(options);
        var publishManaged = HostRoleAssignmentPolicy.PublishingIsManagedByEntra(options);
        var isSelf = string.Equals(actorEmail, email, StringComparison.OrdinalIgnoreCase);

        if (change == HostUserChange.ToggleAdmin && adminManaged)
            return Fail("Administrator access is managed by the configured Microsoft Entra admin group.");
        if (change == HostUserChange.TogglePublish && publishManaged)
            return Fail("Publishing access is managed by the configured Microsoft Entra groups.");
        if (isSelf && change is HostUserChange.ToggleAdmin or HostUserChange.Revoke)
            return Fail("You cannot remove your own administrator access or revoke your own account.");
        if (change is HostUserChange.ToggleAdmin or HostUserChange.TogglePublish
            && (user.IsRevoked || user.ApprovalStatus != HostUserApprovalStatus.Approved))
            return Fail("Restore and approve this account before changing its permissions.");

        if (user.IsAdmin && !user.IsRevoked && change is HostUserChange.ToggleAdmin or HostUserChange.Revoke)
        {
            if (!await db.HostUsers.AnyAsync(u => u.Email != user.Email && u.IsAdmin && !u.IsRevoked
                && u.ApprovalStatus == HostUserApprovalStatus.Approved, cancellationToken))
                return Fail("The last active administrator cannot be removed or revoked.");
        }

        string message;
        switch (change)
        {
            case HostUserChange.Approve:
                if (user.IsRevoked) return Fail("Restore this account before approving it.");
                user.ApprovalStatus = HostUserApprovalStatus.Approved;
                user.CanConsume = user.IsAdmin || user.CanPublish || options.Microsoft.ConsumerRoleGroupIds.Count == 0;
                message = $"Approved '{email}'. Entra-managed permissions are refreshed at the next sign-in.";
                break;
            case HostUserChange.Revoke:
                if (user.IsRevoked) return Fail("This account is already revoked.");
                user.IsRevoked = true;
                user.SessionVersion++;
                foreach (var token in user.Tokens) token.Revoked = true;
                message = $"Revoked '{email}', its sessions, and its API tokens.";
                break;
            case HostUserChange.Restore:
                if (!user.IsRevoked && user.ApprovalStatus != HostUserApprovalStatus.Denied)
                    return Fail("This account does not need restoration.");
                user.IsRevoked = false;
                user.ApprovalStatus = HostUserApprovalStatus.Pending;
                user.IsAdmin = false;
                user.CanPublish = false;
                user.CanConsume = false;
                user.SessionVersion++;
                foreach (var token in user.Tokens) token.Revoked = true;
                message = $"Restored '{email}'. Approve the account to allow a new sign-in. Old API tokens remain revoked.";
                break;
            case HostUserChange.ToggleAdmin:
                user.IsAdmin = !user.IsAdmin;
                if (!publishManaged) user.CanPublish = user.IsAdmin;
                if (user.IsAdmin) user.CanConsume = true;
                message = user.IsAdmin ? $"Granted administrator access to '{email}'." : $"Removed administrator access from '{email}'.";
                break;
            case HostUserChange.TogglePublish:
                if (user.IsAdmin) return Fail("Administrators can publish when publishing is managed locally.");
                user.CanPublish = !user.CanPublish;
                if (user.CanPublish) user.CanConsume = true;
                message = user.CanPublish ? $"Allowed '{email}' to publish." : $"Removed publishing access from '{email}'.";
                break;
            default:
                return Fail("Unknown account action.");
        }

        db.HostAuditEvents.Add(new HostAuditEvent
        {
            Actor = actorEmail, Subject = email, EventType = "user." + change.ToString().ToLowerInvariant(),
            Timestamp = DateTimeOffset.UtcNow,
            Detail = $"admin={user.IsAdmin}; publish={user.CanPublish}; consume={user.CanConsume}; status={user.ApprovalStatus}; revoked={user.IsRevoked}",
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(true, message);
    }

    private static HostUserChangeResult Fail(string message) => new(false, message);
}

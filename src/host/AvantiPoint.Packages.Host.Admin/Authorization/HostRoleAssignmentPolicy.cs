using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Authorization;

/// <summary>
/// Defines whether administrative and publishing roles are managed locally or by
/// Microsoft Entra group membership, and applies the externally-managed roles at sign-in.
/// </summary>
public static class HostRoleAssignmentPolicy
{
    public static bool AdminIsManagedByEntra(HostAuthenticationOptions options) =>
        options.Microsoft.AdminRoleGroupIds.Count > 0;

    public static bool PublisherGroupIsConfigured(HostAuthenticationOptions options) =>
        options.Microsoft.PublisherRoleGroupIds.Count > 0;

    public static bool PublishingIsManagedByEntra(HostAuthenticationOptions options) =>
        PublisherGroupIsConfigured(options) || AdminIsManagedByEntra(options);

    public static void ApplyMicrosoftRoles(
        HostUser user,
        ClaimsPrincipal principal,
        HostAuthenticationOptions options)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(options);

        var microsoft = options.Microsoft;
        var groups = principal.FindAll("groups")
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (AdminIsManagedByEntra(options))
        {
            user.IsAdmin = microsoft.AdminRoleGroupIds.Any(groups.Contains);
        }

        if (PublisherGroupIsConfigured(options))
        {
            // A configured publisher group is authoritative. Membership in an admin group
            // does not silently grant package publishing when the operator separated the roles.
            user.CanPublish = microsoft.PublisherRoleGroupIds.Any(groups.Contains);
        }
        else if (AdminIsManagedByEntra(options))
        {
            // When no dedicated publisher group exists, an Entra-managed admin group is
            // the complete publisher set. Local toggles cannot expand that external policy.
            user.CanPublish = user.IsAdmin;
        }
        else if (user.IsAdmin)
        {
            // Without a publisher group, administrators are publishers by default. A local
            // administrator may still grant publishing to non-admin users from the Users page.
            user.CanPublish = true;
        }

        user.CanConsume = user.IsAdmin
            || user.CanPublish
            || microsoft.ConsumerRoleGroupIds.Count == 0
            || microsoft.ConsumerRoleGroupIds.Any(groups.Contains);
    }
}

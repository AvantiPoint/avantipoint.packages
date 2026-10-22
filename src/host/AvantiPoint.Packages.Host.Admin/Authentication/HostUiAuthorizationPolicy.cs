using Microsoft.AspNetCore.Authorization;

namespace AvantiPoint.Packages.Host.Admin.Authentication;

/// <summary>
/// Keeps the human package browser aligned with the pull policy enforced by the
/// protocol endpoints. Administrative pages add their own, stricter role
/// requirements on top of this policy.
/// </summary>
public static class HostUiAuthorizationPolicy
{
    public const string Name = "UI";

    public static AuthorizationPolicy Create(bool allowAnonymousPull)
    {
        var policy = new AuthorizationPolicyBuilder();
        if (allowAnonymousPull)
        {
            policy.RequireAssertion(static _ => true);
        }
        else
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(FeedRoles.Consumer, FeedRoles.Admin);
        }

        return policy.Build();
    }
}

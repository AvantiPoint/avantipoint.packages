using Microsoft.AspNetCore.Authorization;

namespace AvantiPoint.Packages.Host.Admin.Authentication;

/// <summary>
/// Requires sign-in for the human package browser independently of anonymous
/// protocol pulls. Private feeds retain their consumer/admin role requirement;
/// administrative pages add their own, stricter role requirements.
/// </summary>
public static class HostUiAuthorizationPolicy
{
    public const string Name = "UI";

    public static AuthorizationPolicy Create(bool allowAnonymousPull)
    {
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser();
        if (!allowAnonymousPull)
        {
            policy.RequireRole(FeedRoles.Consumer, FeedRoles.Admin);
        }

        return policy.Build();
    }
}

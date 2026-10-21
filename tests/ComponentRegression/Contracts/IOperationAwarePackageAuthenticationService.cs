using AvantiPoint.Packages.Core;

namespace AvantiPoint.Feed.Platform.Authentication;

/// <summary>
/// Allows a package authentication provider to validate the same credential against
/// the permission required by the current protocol operation.
/// </summary>
public interface IOperationAwarePackageAuthenticationService
{
    Task<NuGetAuthenticationResult> AuthenticateAsync(
        string token,
        FeedOperation operation,
        CancellationToken cancellationToken = default);

    Task<NuGetAuthenticationResult> AuthenticateAsync(
        string username,
        string token,
        FeedOperation operation,
        CancellationToken cancellationToken = default);
}


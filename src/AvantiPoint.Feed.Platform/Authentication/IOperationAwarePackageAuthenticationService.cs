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

public static class OperationAwarePackageAuthenticationExtensions
{
    public static Task<NuGetAuthenticationResult> AuthenticateForOperationAsync(
        this IPackageAuthenticationService authentication,
        string token,
        FeedOperation operation,
        CancellationToken cancellationToken = default) =>
        authentication is IOperationAwarePackageAuthenticationService operationAware
            ? WithLoginFallbackAsync(op => operationAware.AuthenticateAsync(token, op, cancellationToken), operation)
            : authentication.AuthenticateAsync(token, cancellationToken);

    public static Task<NuGetAuthenticationResult> AuthenticateForOperationAsync(
        this IPackageAuthenticationService authentication,
        string username,
        string token,
        FeedOperation operation,
        CancellationToken cancellationToken = default) =>
        authentication is IOperationAwarePackageAuthenticationService operationAware
            ? WithLoginFallbackAsync(op => operationAware.AuthenticateAsync(username, token, op, cancellationToken), operation)
            : authentication.AuthenticateAsync(username, token, cancellationToken);

    private static async Task<NuGetAuthenticationResult> WithLoginFallbackAsync(
        Func<FeedOperation, Task<NuGetAuthenticationResult>> authenticate,
        FeedOperation operation)
    {
        var result = await authenticate(operation);
        if (result.Succeeded || operation != FeedOperation.Login) return result;
        // Existing custom providers may only understand Pull/Push. Either permission
        // permits a registry login probe; artifact routes still request their exact operation.
        result = await authenticate(FeedOperation.Pull);
        return result.Succeeded ? result : await authenticate(FeedOperation.Push);
    }
}

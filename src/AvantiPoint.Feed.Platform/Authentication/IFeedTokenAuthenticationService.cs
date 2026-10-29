namespace AvantiPoint.Feed.Platform.Authentication;

/// <summary>
/// Validates an opaque feed token for the requested operation. Unlike the legacy
/// NuGet overloads, bearer consumption does not imply permission to publish.
/// </summary>
public interface IFeedTokenAuthenticationService
{
    Task<FeedAuthenticationResult> AuthenticateTokenAsync(
        string token, FeedOperation operation, string? username,
        CancellationToken cancellationToken = default);
}

using AvantiPoint.Feed.Platform.Authentication;

namespace AvantiPoint.Packages.UI.Tests;

internal sealed class FirstValidationOnlyNativeAuthentication : IFeedTokenAuthenticationService
{
    public int Calls { get; private set; }

    public Task<FeedAuthenticationResult> AuthenticateTokenAsync(string token, FeedOperation operation,
        string? username, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(Calls == 1 ? FeedAuthenticationResult.Success()
            : FeedAuthenticationResult.Forbidden("Fixture permits a single token validation."));
    }
}

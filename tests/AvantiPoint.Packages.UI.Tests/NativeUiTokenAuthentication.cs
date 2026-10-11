using AvantiPoint.Feed.Platform.Authentication;

namespace AvantiPoint.Packages.UI.Tests;

internal sealed class NativeUiTokenAuthentication : IFeedTokenAuthenticationService
{
    public Task<FeedAuthenticationResult> AuthenticateTokenAsync(string token, FeedOperation operation, string? username, CancellationToken cancellationToken = default)
    {
        var result = token switch
        {
            "reader" when operation == FeedOperation.Pull => FeedAuthenticationResult.Success(),
            "writer" when operation == FeedOperation.Push => FeedAuthenticationResult.Success(),
            "writer" => FeedAuthenticationResult.Forbidden("Write-only fixture token."),
            _ => FeedAuthenticationResult.Fail("Invalid fixture token.", new Dictionary<string, string>
            {
                ["WWW-Authenticate"] = "Bearer realm=\"fixture\", error=\"invalid_token\"",
            }),
        };
        return Task.FromResult(result);
    }
}

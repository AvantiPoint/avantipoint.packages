using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace OpenFeed.Services;

/// <summary>Uses the sample's shared API key for native reads and publication.</summary>
public sealed class OpenFeedTokenAuthenticationService(
    IPackageAuthenticationService authentication,
    IOptionsSnapshot<PackageFeedOptions> options) : IFeedTokenAuthenticationService
{
    public async Task<FeedAuthenticationResult> AuthenticateTokenAsync(
        string token, FeedOperation operation, string? username,
        CancellationToken cancellationToken = default)
    {
        if (operation is not (FeedOperation.Pull or FeedOperation.Push))
            return FeedAuthenticationResult.Forbidden("Unsupported feed operation.");

        // The legacy sample accepts any publisher when no key is configured.
        // Native publication and private reads require an explicitly configured
        // key; anonymous reads remain controlled by AllowAnonymousPull.
        if (string.IsNullOrEmpty(options.Value.ApiKey) || string.IsNullOrEmpty(token))
            return FeedAuthenticationResult.Fail("A configured sample API key is required.");

        var result = await authentication.AuthenticateAsync(token, cancellationToken);
        return result.Succeeded
            ? FeedAuthenticationResult.Success(result.User)
            : FeedAuthenticationResult.Fail(result.Message);
    }
}

using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace AvantiPoint.Packages.Registry.Npm.Authentication;

public sealed class AuthorizedNpmConsumerFilter : AuthorizedNpmFilter
{
    public AuthorizedNpmConsumerFilter(
        ILogger<AuthorizedNpmConsumerFilter> logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
        : base(logger, authentication, surfaceAccessor)
    {
    }

    protected override FeedOperation Operation => FeedOperation.Pull;
}

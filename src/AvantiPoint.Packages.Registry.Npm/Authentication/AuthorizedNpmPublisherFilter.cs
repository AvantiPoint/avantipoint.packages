using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace AvantiPoint.Packages.Registry.Npm.Authentication;

public sealed class AuthorizedNpmPublisherFilter : AuthorizedNpmFilter
{
    public AuthorizedNpmPublisherFilter(
        ILogger<AuthorizedNpmPublisherFilter> logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
        : base(logger, authentication, surfaceAccessor)
    {
    }

    protected override FeedOperation Operation => FeedOperation.Push;
}


using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Authentication;

public sealed class AuthorizedNuGetConsumerFilter : AuthorizedNuGetFilter
{
    public AuthorizedNuGetConsumerFilter(
        ILogger<AuthorizedNuGetConsumerFilter> logger,
        IFeedAuthenticationService authentication,
        ISurfaceContextAccessor surfaceAccessor)
        : base(logger, authentication, surfaceAccessor)
    {
    }

    protected override FeedOperation Operation => FeedOperation.Pull;
}

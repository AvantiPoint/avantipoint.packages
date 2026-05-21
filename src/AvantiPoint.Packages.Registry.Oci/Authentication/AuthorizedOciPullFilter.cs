using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci.Authentication;

public sealed class AuthorizedOciPullFilter(
    ILogger<AuthorizedOciPullFilter> logger,
    IFeedAuthenticationService authentication,
    ISurfaceContextAccessor surfaceAccessor)
    : AuthorizedOciFilter(logger, authentication, surfaceAccessor)
{
    protected override FeedOperation Operation => FeedOperation.Pull;
}

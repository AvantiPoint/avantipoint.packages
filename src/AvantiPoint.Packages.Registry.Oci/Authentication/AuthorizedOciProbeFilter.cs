using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci.Authentication;

/// <summary>
/// The OCI distribution version probe is used by both pull and push clients.
/// A publish-only credential must therefore be able to complete the probe without
/// granting it access to manifests, blobs, tags, or the catalog.
/// </summary>
public sealed class AuthorizedOciProbeFilter(
    ILogger<AuthorizedOciProbeFilter> logger,
    IFeedAuthenticationService authentication,
    ISurfaceContextAccessor surfaceAccessor)
    : AuthorizedOciFilter(logger, authentication, surfaceAccessor)
{
    protected override FeedOperation Operation => FeedOperation.Login;
}

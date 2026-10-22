using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Feed.Platform.Configuration;

public class OciMirrorOptions
{
    public IList<OciUpstreamRegistryOptions> Registries { get; set; } = [];

    public MirrorCachingStrategy CachingStrategy { get; set; } = MirrorCachingStrategy.IndexAndCache;

    /// <summary>
    /// How often an OCI repository or mutable tag that was previously consumed from an
    /// upstream registry may be checked for updates. Refreshes are demand-driven and never
    /// enumerate the upstream registry catalog.
    /// </summary>
    public TimeSpan MetadataRefreshInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Enables update checks for repositories and tags previously consumed from an
    /// upstream registry. Referenced blobs remain lazy and are fetched only when requested.
    /// </summary>
    public bool RefreshTrackedArtifacts { get; set; } = true;

    /// <summary>
    /// Maximum number of tags requested from a tracked Helm repository during one update
    /// check. This bounds discovery to a single page from that repository.
    /// </summary>
    public int TrackedTagDiscoveryLimit { get; set; } = 256;

    /// <summary>
    /// Maximum number of newly discovered Helm manifests indexed during one update check.
    /// Chart blobs are not downloaded by discovery.
    /// </summary>
    public int NewHelmVersionsPerRefresh { get; set; } = 16;
}

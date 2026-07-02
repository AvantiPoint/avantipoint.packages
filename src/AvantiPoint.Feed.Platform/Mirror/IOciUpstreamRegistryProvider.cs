using AvantiPoint.Feed.Platform.Configuration;

namespace AvantiPoint.Feed.Platform.Mirror;

/// <summary>
/// Supplies the upstream OCI registries used for pull-through mirroring of a surface. The
/// default implementation reads static configuration; hosts may replace it with a
/// database-backed provider so upstreams can be managed at runtime.
/// </summary>
public interface IOciUpstreamRegistryProvider
{
    ValueTask<IReadOnlyList<OciUpstreamRegistryOptions>> GetRegistriesAsync(SurfaceContext surface, CancellationToken cancellationToken = default);
}

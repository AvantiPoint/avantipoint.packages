using AvantiPoint.Feed.Platform.Configuration;

namespace AvantiPoint.Feed.Platform.Mirror;

/// <summary>
/// Supplies the upstream npm registries used for pull-through mirroring. The default
/// implementation reads static configuration; hosts may replace it with a database-backed
/// provider so upstreams can be managed at runtime.
/// </summary>
public interface INpmUpstreamRegistryProvider
{
    ValueTask<IReadOnlyList<NpmUpstreamRegistryOptions>> GetRegistriesAsync(CancellationToken cancellationToken = default);
}

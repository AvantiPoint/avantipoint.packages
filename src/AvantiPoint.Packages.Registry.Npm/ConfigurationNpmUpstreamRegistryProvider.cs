using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Npm;

/// <summary>
/// Default provider that reads upstream npm registries from static configuration
/// (<c>Feed:Npm:Mirror</c>). Hosts may replace this with a database-backed provider.
/// </summary>
public sealed class ConfigurationNpmUpstreamRegistryProvider(IOptions<NpmFeedOptions> options)
    : INpmUpstreamRegistryProvider
{
    public ValueTask<IReadOnlyList<NpmUpstreamRegistryOptions>> GetRegistriesAsync(
        CancellationToken cancellationToken = default) =>
        new(options.Value.Mirror?.GetUpstreamRegistries() ?? []);
}

using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Default provider that reads upstream OCI registries from static configuration
/// (<c>Feed:*:Mirror:Registries</c>). Hosts may replace this with a database-backed provider.
/// </summary>
public sealed class ConfigurationOciUpstreamRegistryProvider(OciFeedOptionsAccessor optionsAccessor)
    : IOciUpstreamRegistryProvider
{
    public ValueTask<IReadOnlyList<OciUpstreamRegistryOptions>> GetRegistriesAsync(
        SurfaceContext surface,
        CancellationToken cancellationToken = default)
    {
        var options = optionsAccessor.GetOptions(surface);
        IReadOnlyList<OciUpstreamRegistryOptions> registries = options.Mirror?.Registries?
            .Where(r => !string.IsNullOrWhiteSpace(r.Url))
            .OrderBy(r => r.Priority)
            .ToArray()
            ?? [];

        return new(registries);
    }
}

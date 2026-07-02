using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Feed.Platform.Configuration;

public class NpmMirrorOptions
{
    /// <summary>
    /// Single upstream registry shorthand (unauthenticated). Ignored when
    /// <see cref="Registries"/> has entries.
    /// </summary>
    public string RegistryUrl { get; set; } = "https://registry.npmjs.org";

    /// <summary>
    /// Upstream registries tried in ascending <see cref="NpmUpstreamRegistryOptions.Priority"/>
    /// order, with optional per-registry credentials. When empty, falls back to
    /// <see cref="RegistryUrl"/>.
    /// </summary>
    public List<NpmUpstreamRegistryOptions> Registries { get; set; } = [];

    public MirrorCachingStrategy CachingStrategy { get; set; } = MirrorCachingStrategy.IndexAndCache;

    /// <summary>
    /// The effective upstream registries: <see cref="Registries"/> ordered by priority,
    /// or a single unauthenticated registry built from <see cref="RegistryUrl"/>.
    /// </summary>
    public IReadOnlyList<NpmUpstreamRegistryOptions> GetUpstreamRegistries()
    {
        var configured = Registries
            .Where(static r => !string.IsNullOrWhiteSpace(r.Url))
            .OrderBy(static r => r.Priority)
            .ToList();

        if (configured.Count > 0)
        {
            return configured;
        }

        if (string.IsNullOrWhiteSpace(RegistryUrl))
        {
            return [];
        }

        return [new NpmUpstreamRegistryOptions { Url = RegistryUrl }];
    }
}

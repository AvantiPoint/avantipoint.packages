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
    /// How often a package already consumed from an upstream registry may have its
    /// packument checked for newer versions. Only metadata for that package is fetched;
    /// tarballs remain lazy and are downloaded only when requested.
    /// </summary>
    public TimeSpan MetadataRefreshInterval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Enables metadata refresh for packages previously discovered from an upstream.
    /// This never crawls the upstream catalog.
    /// </summary>
    public bool RefreshTrackedPackages { get; set; } = true;

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

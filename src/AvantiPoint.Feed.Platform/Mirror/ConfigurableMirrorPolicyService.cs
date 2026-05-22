using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Feed.Platform.Mirror;

/// <summary>
/// Applies per-protocol discovery and caching policy from feed configuration.
/// </summary>
public sealed class ConfigurableMirrorPolicyService : IMirrorPolicyService
{
    private readonly SearchOptions _searchOptions;
    private readonly NpmFeedOptions _npmOptions;
    private readonly IOptionsMonitor<OciFeedOptions> _ociOptionsMonitor;

    public ConfigurableMirrorPolicyService(
        IOptions<SearchOptions> searchOptions,
        IOptions<NpmFeedOptions> npmOptions,
        IOptionsMonitor<OciFeedOptions> ociOptionsMonitor)
    {
        _searchOptions = searchOptions?.Value ?? new SearchOptions();
        _npmOptions = npmOptions?.Value ?? new NpmFeedOptions();
        _ociOptionsMonitor = ociOptionsMonitor;
    }

    public MirrorCachingStrategy GetStrategy(FeedProtocol protocol, string? surfaceId = null) =>
        MirrorCachingStrategy.IndexAndCache;

    public bool IncludeInDiscovery(FeedProtocol protocol, PackageOrigin origin, string? surfaceId = null) =>
        origin switch
        {
            PackageOrigin.Published => true,
            PackageOrigin.Cached => false,
            PackageOrigin.Mirrored => protocol switch
            {
                FeedProtocol.NuGet => _searchOptions.IncludeMirroredPackages,
                FeedProtocol.Npm => _npmOptions.IncludeMirroredPackages,
                FeedProtocol.Oci => GetOciOptions(surfaceId).IncludeMirroredInCatalog,
                _ => false,
            },
            _ => false,
        };

    private OciFeedOptions GetOciOptions(string? surfaceId) =>
        string.IsNullOrEmpty(surfaceId)
            ? _ociOptionsMonitor.Get("OciFeed:default")
            : _ociOptionsMonitor.Get(OciSurfaceOptionsBuilder.GetOptionsName(surfaceId));
}

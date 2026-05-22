using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Feed.Platform.Mirror;

public enum MirrorCachingStrategy
{
    IndexAndCache,
    CacheOnly,
    ProxyOnly,
}

public interface IMirrorPolicyService
{
    MirrorCachingStrategy GetStrategy(FeedProtocol protocol, string? surfaceId = null);

    bool IncludeInDiscovery(FeedProtocol protocol, PackageOrigin origin, string? surfaceId = null);
}

public sealed class DefaultMirrorPolicyService : IMirrorPolicyService
{
    public MirrorCachingStrategy GetStrategy(FeedProtocol protocol, string? surfaceId = null) =>
        MirrorCachingStrategy.IndexAndCache;

    public bool IncludeInDiscovery(FeedProtocol protocol, PackageOrigin origin, string? surfaceId = null) =>
        origin switch
        {
            PackageOrigin.Published => true,
            PackageOrigin.Mirrored => false,
            PackageOrigin.Cached => false,
            _ => false,
        };
}

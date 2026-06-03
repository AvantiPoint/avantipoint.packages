using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Feed.Platform.Configuration;

public class OciMirrorOptions
{
    public IList<OciUpstreamRegistryOptions> Registries { get; set; } = [];

    public MirrorCachingStrategy CachingStrategy { get; set; } = MirrorCachingStrategy.IndexAndCache;
}

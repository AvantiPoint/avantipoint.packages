using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Feed.Platform.Configuration;

public class NpmMirrorOptions
{
    public string RegistryUrl { get; set; } = "https://registry.npmjs.org";

    public MirrorCachingStrategy CachingStrategy { get; set; } = MirrorCachingStrategy.IndexAndCache;
}

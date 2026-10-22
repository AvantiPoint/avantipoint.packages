using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Feed.Platform.Configuration;

public class OciUpstreamRegistryOptions
{
    public string Url { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public int Priority { get; set; }

    /// <summary>
    /// Optional policy for artifacts served by this registry. When omitted, the OCI
    /// surface's <see cref="OciMirrorOptions.CachingStrategy"/> is used.
    /// </summary>
    public MirrorCachingStrategy? CachingStrategy { get; set; }
}

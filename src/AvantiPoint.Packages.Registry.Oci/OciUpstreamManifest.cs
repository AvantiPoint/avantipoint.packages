using AvantiPoint.Feed.Platform.Mirror;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed record OciUpstreamManifest(string Digest, string MediaType, byte[] Content)
{
    /// <summary>
    /// Policy from the upstream registry that actually returned this manifest. A null
    /// value preserves compatibility with custom mirror implementations and falls back
    /// to the surface-level policy.
    /// </summary>
    public MirrorCachingStrategy? CachingStrategy { get; init; }
}

public sealed record OciUpstreamBlob(Stream Content, MirrorCachingStrategy CachingStrategy)
{
    /// <summary>
    /// The upstream content length when it was supplied by the registry. A null value
    /// means the response is streamed with chunked or otherwise unknown length.
    /// </summary>
    public long? Size { get; init; }
}

public sealed record OciUpstreamTagList(
    IReadOnlyList<string> Tags,
    MirrorCachingStrategy CachingStrategy);

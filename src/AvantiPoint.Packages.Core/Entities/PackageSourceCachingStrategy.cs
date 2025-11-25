namespace AvantiPoint.Packages.Core;

/// <summary>
/// Determines how mirrored packages from a source are stored locally.
/// </summary>
public enum PackageSourceCachingStrategy
{
    /// <summary>
    /// Download, cache, and index packages locally (default behavior).
    /// </summary>
    IndexAndCache,

    /// <summary>
    /// Cache package binaries locally for restore but omit them from search and discovery.
    /// </summary>
    CacheOnly,

    /// <summary>
    /// Do not cache or index packages; respond to requests by proxying upstream.
    /// </summary>
    ProxyOnly
}


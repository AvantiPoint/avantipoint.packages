#nullable enable

using System.IO;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Represents the outcome of attempting to mirror a package for a specific source.
/// </summary>
public class MirrorOperationResult
{
    private MirrorOperationResult(bool hasLocalCopy, Stream? directStream, PackageSource? source)
    {
        HasLocalCopy = hasLocalCopy;
        DirectStream = directStream;
        Source = source;
    }

    public bool HasLocalCopy { get; }

    /// <summary>
    /// When non-null, callers should stream the package directly to the client without caching.
    /// The caller is responsible for disposing this stream.
    /// </summary>
    public Stream? DirectStream { get; }

    /// <summary>
    /// Gets a directly streamed upstream response. Retained for compatibility with callers that
    /// predate local-cache streaming.
    /// </summary>
    public Stream? ProxiedStream => DirectStream;

    public PackageSource? Source { get; }

    public bool HasDirectStream => DirectStream is not null;

    public bool IsProxied => DirectStream is not null && Source is not null;

    public bool Found => HasLocalCopy || HasDirectStream;

    public static MirrorOperationResult NotFound { get; } = new(false, null, null);

    public static MirrorOperationResult AlreadyAvailable { get; } = new(true, null, null);

    public static MirrorOperationResult Stored(PackageSource source)
        => new(true, null, source);

    public static MirrorOperationResult StoredFromLocalCache { get; } = new(true, null, null);

    public static MirrorOperationResult Proxied(PackageSource source, Stream stream)
        => new(false, stream, source);

    public static MirrorOperationResult Direct(Stream stream)
        => new(false, stream, null);
}

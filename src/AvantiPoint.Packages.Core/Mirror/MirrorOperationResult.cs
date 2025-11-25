#nullable enable

using System.IO;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Represents the outcome of attempting to mirror a package for a specific source.
/// </summary>
public class MirrorOperationResult
{
    private MirrorOperationResult(bool hasLocalCopy, Stream? proxiedStream, PackageSource? source)
    {
        HasLocalCopy = hasLocalCopy;
        ProxiedStream = proxiedStream;
        Source = source;
    }

    public bool HasLocalCopy { get; }

    /// <summary>
    /// When non-null, callers should stream the package directly to the client without caching.
    /// The caller is responsible for disposing this stream.
    /// </summary>
    public Stream? ProxiedStream { get; }

    public PackageSource? Source { get; }

    public bool IsProxied => ProxiedStream is not null;

    public bool Found => HasLocalCopy || IsProxied;

    public static MirrorOperationResult NotFound { get; } = new(false, null, null);

    public static MirrorOperationResult AlreadyAvailable { get; } = new(true, null, null);

    public static MirrorOperationResult Stored(PackageSource source)
        => new(true, null, source);

    public static MirrorOperationResult Proxied(PackageSource source, Stream stream)
        => new(false, stream, source);
}


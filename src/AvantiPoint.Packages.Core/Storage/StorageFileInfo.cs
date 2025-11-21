using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Minimal metadata and helpers for a file stored via <see cref="IStorageService"/>.
/// </summary>
public sealed class StorageFileInfo(
    IStorageService storage,
    string path,
    DateTimeOffset? lastModifiedUtc)
{

    /// <summary>
    /// Provider-relative storage path (e.g., "packages/id/version/...").
    /// </summary>
    public string Path { get; } = path;

    /// <summary>
    /// Last time the file was modified in this storage system, if known.
    /// </summary>
    public DateTimeOffset? LastModifiedUtc { get; } = lastModifiedUtc;

    /// <summary>
    /// True if the file exists (i.e., we have a last-modified value).
    /// </summary>
    public bool Exists => LastModifiedUtc is not null;

    /// <summary>
    /// Open a read-only stream for this file using the underlying storage service.
    /// </summary>
    public Task<Stream> OpenReadAsync(CancellationToken cancellationToken = default)
        => storage.GetAsync(Path, cancellationToken);

    /// <summary>
    /// Move this file to a new path by copying then deleting the original.
    /// Content type is preserved as generic binary; callers can re-upload with
    /// a more specific content type if needed.
    /// </summary>
    public async Task MoveAsync(string newPath, CancellationToken cancellationToken = default)
    {
        using var stream = await storage.GetAsync(Path, cancellationToken);
        await storage.PutAsync(newPath, stream, "application/octet-stream", cancellationToken);
        await storage.DeleteAsync(Path, cancellationToken);
    }

    /// <summary>
    /// Delete this file using the underlying storage service.
    /// </summary>
    public Task DeleteAsync(CancellationToken cancellationToken = default)
        => storage.DeleteAsync(Path, cancellationToken);
}



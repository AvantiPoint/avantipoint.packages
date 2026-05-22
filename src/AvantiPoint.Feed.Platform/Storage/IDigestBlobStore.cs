namespace AvantiPoint.Feed.Platform.Storage;

/// <summary>
/// Digest-addressed blob storage for OCI (implementation in OCI registry phase).
/// </summary>
public interface IDigestBlobStore
{
    Task PutAsync(string algorithm, string hex, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> GetAsync(string algorithm, string hex, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string algorithm, string hex, CancellationToken cancellationToken = default);

    Task DeleteAsync(string algorithm, string hex, CancellationToken cancellationToken = default);
}

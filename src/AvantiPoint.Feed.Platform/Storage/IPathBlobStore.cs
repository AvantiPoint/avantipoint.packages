namespace AvantiPoint.Feed.Platform.Storage;

public interface IPathBlobStore
{
    Task PutAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> GetAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default);
}

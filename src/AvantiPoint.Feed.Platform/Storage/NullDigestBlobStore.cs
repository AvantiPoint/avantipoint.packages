using AvantiPoint.Packages.Core;
namespace AvantiPoint.Feed.Platform.Storage;

internal sealed class NullDigestBlobStore : IDigestBlobStore
{
    public Task PutAsync(string algorithm, string hex, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OCI digest blob store is not configured.");

    public Task<Stream> GetAsync(string algorithm, string hex, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OCI digest blob store is not configured.");

    public Task<bool> ExistsAsync(string algorithm, string hex, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

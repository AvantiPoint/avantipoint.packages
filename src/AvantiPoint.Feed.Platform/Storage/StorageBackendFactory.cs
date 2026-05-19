using AvantiPoint.Packages.Core;

namespace AvantiPoint.Feed.Platform.Storage;

public sealed class StorageBackendFactory : IStorageBackendFactory
{
    private readonly IStorageService _storage;
    private readonly string _feedPrefix;

    public StorageBackendFactory(IStorageService storage, IFeedRegistry registry)
    {
        _storage = storage;
        _feedPrefix = registry.Feed.StoragePrefix ?? string.Empty;
    }

    public IPathBlobStore CreatePathStore(string subPrefix) =>
        new PathBlobStore(_storage, Combine(_feedPrefix, subPrefix));

    public IDigestBlobStore CreateDigestStore(string subPrefix) =>
        new NullDigestBlobStore();

    private static string Combine(string a, string b)
    {
        if (string.IsNullOrEmpty(a))
        {
            return b ?? string.Empty;
        }

        if (string.IsNullOrEmpty(b))
        {
            return a;
        }

        return $"{a.TrimEnd('/')}/{b.Trim('/')}/";
    }
}

internal sealed class NullDigestBlobStore : IDigestBlobStore
{
    public Task PutAsync(string algorithm, string hex, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OCI digest blob store is not configured.");

    public Task<Stream> GetAsync(string algorithm, string hex, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("OCI digest blob store is not configured.");

    public Task<bool> ExistsAsync(string algorithm, string hex, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}

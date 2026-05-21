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


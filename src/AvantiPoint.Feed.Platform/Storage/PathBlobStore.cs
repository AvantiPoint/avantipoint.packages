using AvantiPoint.Packages.Core;

namespace AvantiPoint.Feed.Platform.Storage;

public sealed class PathBlobStore : IPathBlobStore
{
    private readonly IStorageService _storage;
    private readonly string _prefix;

    public PathBlobStore(IStorageService storage, string prefix)
    {
        _storage = storage;
        _prefix = NormalizePrefix(prefix);
    }

    public async Task PutAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(relativePath);
        await _storage.PutAsync(path, content, "application/octet-stream", cancellationToken);
    }

    public Task<Stream> GetAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(relativePath);
        return _storage.GetAsync(path, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(relativePath);
        try
        {
            await using var stream = await _storage.GetAsync(path, cancellationToken);
            return stream is not null;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public Task DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(relativePath);
        return _storage.DeleteAsync(path, cancellationToken);
    }

    private string ToStoragePath(string relativePath)
    {
        var normalized = relativePath.TrimStart('/', '\\').Replace('\\', '/');
        return string.IsNullOrEmpty(_prefix) ? normalized : $"{_prefix}{normalized}";
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return string.Empty;
        }

        var normalized = prefix.Replace('\\', '/').Trim('/');
        return string.IsNullOrEmpty(normalized) ? string.Empty : normalized + "/";
    }
}

using System.Security.Cryptography;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Feed.Platform.Storage;

public sealed class DigestBlobStore : IDigestBlobStore
{
    private readonly IStorageService _storage;
    private readonly string _prefix;

    public DigestBlobStore(IStorageService storage, string prefix)
    {
        _storage = storage;
        _prefix = NormalizePrefix(prefix);
    }

    public async Task PutAsync(string algorithm, string hex, Stream content, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(algorithm, hex);
        await _storage.PutAsync(path, content, "application/octet-stream", cancellationToken);
    }

    public Task<Stream> GetAsync(string algorithm, string hex, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(algorithm, hex);
        return _storage.GetAsync(path, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string algorithm, string hex, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(algorithm, hex);
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

    public Task DeleteAsync(string algorithm, string hex, CancellationToken cancellationToken = default)
    {
        var path = ToStoragePath(algorithm, hex);
        return _storage.DeleteAsync(path, cancellationToken);
    }

    public static (string Algorithm, string Hex) ParseDigest(string digest)
    {
        var separator = digest.IndexOf(':');
        if (separator <= 0 || separator >= digest.Length - 1)
        {
            throw new ArgumentException($"Invalid digest format: '{digest}'.", nameof(digest));
        }

        return (digest[..separator], digest[(separator + 1)..]);
    }

    public static async Task<string> ComputeSha256DigestAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(content, cancellationToken);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private string ToStoragePath(string algorithm, string hex) =>
        string.IsNullOrEmpty(_prefix)
            ? $"v2/blobs/{algorithm}/{hex}/data"
            : $"{_prefix}v2/blobs/{algorithm}/{hex}/data";

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

using System.Net;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Gcp.Storage;

public class GcsStorageService : IStorageService
{
    private const string Separator = "/";
    private readonly string _bucket;
    private readonly string _prefix;
    private readonly StorageClient _client;
    private readonly GcsStorageOptions _options;

    public GcsStorageService(IOptionsSnapshot<GcsStorageOptions> options, StorageClient client)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _client = client ?? throw new ArgumentNullException(nameof(client));

        if (string.IsNullOrWhiteSpace(_options.Bucket))
        {
            throw new ArgumentException("Bucket is required.", nameof(options));
        }

        _bucket = _options.Bucket;
        _prefix = _options.Prefix ?? string.Empty;
        if (!string.IsNullOrEmpty(_prefix) && !_prefix.EndsWith(Separator, StringComparison.Ordinal))
        {
            _prefix += Separator;
        }
    }

    public async IAsyncEnumerable<StorageFileInfo> ListFilesAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var objectPrefix = BuildObjectPrefix(prefix);

        if (_options.UseEmulator && !string.IsNullOrWhiteSpace(_options.EmulatorHost))
        {
            var emulatorBaseUri = GetEmulatorBaseUri();
            var objects = await GcsEmulatorClient.ListObjectsAsync(
                emulatorBaseUri,
                _bucket,
                objectPrefix,
                cancellationToken);

            foreach (var obj in objects)
            {
                var key = obj.Name;
                if (!string.IsNullOrEmpty(_prefix) && key.StartsWith(_prefix, StringComparison.Ordinal))
                {
                    key = key.Substring(_prefix.Length);
                }

                var lastModified = obj.Updated?.UtcDateTime ?? DateTime.UtcNow;
                yield return new StorageFileInfo(this, key, lastModified);
            }

            yield break;
        }

        await foreach (var obj in _client.ListObjectsAsync(_bucket, objectPrefix)
                           .WithCancellation(cancellationToken))
        {
            var key = obj.Name;
            if (!string.IsNullOrEmpty(_prefix) && key.StartsWith(_prefix, StringComparison.Ordinal))
            {
                key = key.Substring(_prefix.Length);
            }

            var lastModified = obj.UpdatedDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow;
            yield return new StorageFileInfo(this, key, lastModified);
        }
    }

    public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_options.UseEmulator && !string.IsNullOrWhiteSpace(_options.EmulatorHost))
        {
            return await DownloadViaEmulatorAsync(path, cancellationToken);
        }

        var stream = new MemoryStream();
        try
        {
            await _client.DownloadObjectAsync(_bucket, PrepareObjectName(path), stream, cancellationToken: cancellationToken);
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            stream.Dispose();
            throw new FileNotFoundException($"Object '{path}' not found in bucket '{_bucket}'.", ex);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private Task<Stream> DownloadViaEmulatorAsync(string path, CancellationToken cancellationToken)
    {
        return GcsEmulatorClient.DownloadObjectAsync(
            GetEmulatorBaseUri(),
            _bucket,
            PrepareObjectName(path),
            cancellationToken);
    }

    public async Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_options.UseEmulator && !string.IsNullOrWhiteSpace(_options.EmulatorHost))
        {
            return null!;
        }

        var credential = await GoogleCredential.GetApplicationDefaultAsync(cancellationToken);
        var urlSigner = UrlSigner.FromCredential(credential);
        var url = await urlSigner.SignAsync(
            _bucket,
            PrepareObjectName(path),
            TimeSpan.FromHours(1),
            cancellationToken: cancellationToken);

        return new Uri(url);
    }

    public async Task<StoragePutResult> PutAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var objectName = PrepareObjectName(path);

        if (await ObjectExistsAsync(objectName, cancellationToken))
        {
            await using var existing = _options.UseEmulator && !string.IsNullOrWhiteSpace(_options.EmulatorHost)
                ? await GcsEmulatorClient.DownloadObjectAsync(
                    GetEmulatorBaseUri(),
                    _bucket,
                    objectName,
                    cancellationToken)
                : await DownloadExistingAsync(objectName, cancellationToken);

            content.Position = 0;
            return content.Matches(existing)
                ? StoragePutResult.AlreadyExists
                : StoragePutResult.Conflict;
        }

        using var seekable = new MemoryStream();
        await content.CopyToAsync(seekable, cancellationToken);
        seekable.Seek(0, SeekOrigin.Begin);

        await _client.UploadObjectAsync(
            _bucket,
            objectName,
            contentType,
            seekable,
            new UploadObjectOptions { ChunkSize = null },
            cancellationToken: cancellationToken);

        return StoragePutResult.Success;
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (_options.UseEmulator && !string.IsNullOrWhiteSpace(_options.EmulatorHost))
        {
            return GcsEmulatorClient.DeleteObjectAsync(
                GetEmulatorBaseUri(),
                _bucket,
                PrepareObjectName(path),
                cancellationToken);
        }

        return _client.DeleteObjectAsync(_bucket, PrepareObjectName(path), cancellationToken: cancellationToken);
    }

    private async Task<Stream> DownloadExistingAsync(string objectName, CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();
        await _client.DownloadObjectAsync(_bucket, objectName, stream, cancellationToken: cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        return stream;
    }

    private Task<bool> ObjectExistsAsync(string objectName, CancellationToken cancellationToken)
    {
        if (_options.UseEmulator && !string.IsNullOrWhiteSpace(_options.EmulatorHost))
        {
            return GcsEmulatorClient.ObjectExistsAsync(
                GetEmulatorBaseUri(),
                _bucket,
                objectName,
                cancellationToken);
        }

        return ObjectExistsInProductionAsync(objectName, cancellationToken);
    }

    private async Task<bool> ObjectExistsInProductionAsync(string objectName, CancellationToken cancellationToken)
    {
        try
        {
            _ = await _client.GetObjectAsync(_bucket, objectName, cancellationToken: cancellationToken);
            return true;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private Uri GetEmulatorBaseUri() =>
        new(GcsStorageClientFactory.NormalizeEmulatorHost(_options.EmulatorHost!));

    private string PrepareObjectName(string path) => _prefix + path.Replace("\\", Separator);

    private string BuildObjectPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return _prefix;
        }

        return _prefix + prefix.TrimStart('/').Replace("\\", Separator);
    }
}

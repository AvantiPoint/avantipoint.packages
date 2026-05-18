using AvantiPoint.Packages.Core;
using FluentFTP;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Ftp.Storage;

public class FtpStorageService : IStorageService, IDisposable
{
    private const int DefaultCopyBufferSize = 81920;
    private readonly FtpStorageOptions _options;
    private readonly string _remoteRoot;
    private readonly SemaphoreSlim _connectionGate = new(1, 1);

    public FtpStorageService(IOptionsSnapshot<FtpStorageOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _remoteRoot = NormalizeRemoteRoot(_options.RemotePath);
    }

    public async IAsyncEnumerable<StorageFileInfo> ListFilesAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            using var client = CreateClient();
            await client.Connect(cancellationToken);

            var directory = MapToRemoteDirectory(prefix);
            if (!await client.DirectoryExists(directory, cancellationToken))
            {
                yield break;
            }

            var items = await client.GetListing(directory, FtpListOption.Recursive, cancellationToken);
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (item.Type != FtpObjectType.File)
                {
                    continue;
                }

                var relative = ToRelativePath(item.FullName);
                yield return new StorageFileInfo(this, relative, item.Modified);
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task<Stream> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        var stream = new MemoryStream();
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            using var client = CreateClient();
            await client.Connect(cancellationToken);
            var remotePath = MapToRemoteFile(path);
            if (!await client.FileExists(remotePath, cancellationToken))
            {
                throw new FileNotFoundException($"FTP object '{path}' was not found.");
            }

            var success = await client.DownloadStream(stream, remotePath, token: cancellationToken);
            if (!success)
            {
                throw new IOException($"Failed to download '{path}' from FTP.");
            }

            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public Task<Uri> GetDownloadUriAsync(string path, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Uri>(null!);
    }

    public async Task<StoragePutResult> PutAsync(
        string path,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            using var client = CreateClient();
            await client.Connect(cancellationToken);
            var remotePath = MapToRemoteFile(path);
            await EnsureRemoteDirectory(client, remotePath, cancellationToken);

            if (await client.FileExists(remotePath, cancellationToken))
            {
                using var existing = new MemoryStream();
                await client.DownloadStream(existing, remotePath, token: cancellationToken);
                existing.Seek(0, SeekOrigin.Begin);
                content.Position = 0;
                return content.Matches(existing)
                    ? StoragePutResult.AlreadyExists
                    : StoragePutResult.Conflict;
            }

            using var upload = new MemoryStream();
            await content.CopyToAsync(upload, DefaultCopyBufferSize, cancellationToken);
            upload.Seek(0, SeekOrigin.Begin);

            var status = await client.UploadStream(upload, remotePath, FtpRemoteExists.Overwrite, true, token: cancellationToken);
            if (status is not FtpStatus.Success and not FtpStatus.Skipped)
            {
                throw new IOException($"Failed to upload '{path}' to FTP. Status: {status}");
            }

            return StoragePutResult.Success;
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            using var client = CreateClient();
            await client.Connect(cancellationToken);
            var remotePath = MapToRemoteFile(path);
            if (await client.FileExists(remotePath, cancellationToken))
            {
                await client.DeleteFile(remotePath, cancellationToken);
            }
        }
        finally
        {
            _connectionGate.Release();
        }
    }

    public void Dispose()
    {
        _connectionGate.Dispose();
    }

    private AsyncFtpClient CreateClient()
    {
        var client = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);
        client.Config.ConnectTimeout = (int)_options.ConnectTimeout.TotalMilliseconds;
        client.Config.ReadTimeout = (int)_options.ReadTimeout.TotalMilliseconds;
        client.Config.DataConnectionType = _options.UsePassiveMode
            ? FtpDataConnectionType.PASV
            : FtpDataConnectionType.PORT;

        if (_options.UseSsl)
        {
            client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        }

        return client;
    }

    private static string NormalizeRemoteRoot(string remotePath)
    {
        var root = (remotePath ?? "/").Replace('\\', '/');
        if (!root.StartsWith('/'))
        {
            root = "/" + root;
        }

        return root.TrimEnd('/');
    }

    private string MapToRemoteFile(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        return string.IsNullOrEmpty(_remoteRoot)
            ? normalized
            : $"{_remoteRoot}/{normalized}";
    }

    private string MapToRemoteDirectory(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.IsNullOrEmpty(_remoteRoot) ? "." : _remoteRoot;
        }

        var normalized = prefix.Replace('\\', '/').TrimStart('/');
        return string.IsNullOrEmpty(_remoteRoot)
            ? normalized.TrimEnd('/')
            : $"{_remoteRoot}/{normalized}".TrimEnd('/');
    }

    private string ToRelativePath(string fullName)
    {
        var normalized = fullName.Replace('\\', '/');
        if (normalized.StartsWith(_remoteRoot, StringComparison.Ordinal))
        {
            normalized = normalized.Substring(_remoteRoot.Length).TrimStart('/');
        }

        return normalized;
    }

    private static async Task EnsureRemoteDirectory(
        AsyncFtpClient client,
        string remotePath,
        CancellationToken cancellationToken)
    {
        var directory = remotePath.Replace('\\', '/');
        var lastSlash = directory.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return;
        }

        directory = directory[..lastSlash];
        await client.CreateDirectory(directory, true, cancellationToken);
    }
}

using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace AvantiPoint.Packages.Sftp.Storage;

public class SftpStorageService : IStorageService, IDisposable
{
    private const int DefaultCopyBufferSize = 81920;
    private readonly SftpStorageOptions _options;
    private readonly SftpConnectionFactory _connectionFactory;
    private readonly string _remoteRoot;
    private readonly SemaphoreSlim _connectionGate;

    public SftpStorageService(IOptionsSnapshot<SftpStorageOptions> options)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _connectionFactory = new SftpConnectionFactory(Microsoft.Extensions.Options.Options.Create(_options));
        _remoteRoot = _connectionFactory.NormalizeRemoteRoot();
        _connectionGate = new SemaphoreSlim(Math.Max(1, _options.MaxConnections));
    }

    public async IAsyncEnumerable<StorageFileInfo> ListFilesAsync(
        string prefix,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _connectionGate.WaitAsync(cancellationToken);
        try
        {
            using var client = _connectionFactory.CreateClient();
            Connect(client);
            var directory = MapToRemoteDirectory(prefix);

            if (!client.Exists(directory))
            {
                yield break;
            }

            foreach (var file in EnumerateFilesRecursive(client, directory, cancellationToken))
            {
                var relative = ToRelativePath(file.FullName);
                yield return new StorageFileInfo(this, relative, file.LastWriteTimeUtc);
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
            using var client = _connectionFactory.CreateClient();
            Connect(client);
            var remotePath = MapToRemoteFile(path);
            if (!client.Exists(remotePath))
            {
                throw new FileNotFoundException($"SFTP object '{path}' was not found.");
            }

            client.DownloadFile(remotePath, stream);
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
            using var client = _connectionFactory.CreateClient();
            Connect(client);
            var remotePath = MapToRemoteFile(path);
            EnsureRemoteDirectory(client, remotePath);

            if (client.Exists(remotePath))
            {
                using var existing = new MemoryStream();
                client.DownloadFile(remotePath, existing);
                existing.Seek(0, SeekOrigin.Begin);
                content.Position = 0;
                return content.Matches(existing)
                    ? StoragePutResult.AlreadyExists
                    : StoragePutResult.Conflict;
            }

            using var upload = new MemoryStream();
            await content.CopyToAsync(upload, DefaultCopyBufferSize, cancellationToken);
            upload.Seek(0, SeekOrigin.Begin);
            client.UploadFile(upload, remotePath, true);
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
            using var client = _connectionFactory.CreateClient();
            Connect(client);
            var remotePath = MapToRemoteFile(path);
            if (client.Exists(remotePath))
            {
                client.DeleteFile(remotePath);
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

    private static void Connect(SftpClient client) =>
        SftpOperationRetry.Execute(client.Connect);

    private static IEnumerable<ISftpFile> EnumerateFilesRecursive(
        SftpClient client,
        string directory,
        CancellationToken cancellationToken)
    {
        foreach (var entry in client.ListDirectory(directory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsDirectory)
            {
                if (entry.Name is "." or "..")
                {
                    continue;
                }

                foreach (var nested in EnumerateFilesRecursive(client, entry.FullName, cancellationToken))
                {
                    yield return nested;
                }
            }
            else
            {
                yield return entry;
            }
        }
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

    private static void EnsureRemoteDirectory(SftpClient client, string remotePath)
    {
        var directory = remotePath.Replace('\\', '/');
        var lastSlash = directory.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return;
        }

        directory = directory[..lastSlash];
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var isAbsolute = directory.StartsWith('/');
        var parts = directory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = isAbsolute ? "/" : string.Empty;

        foreach (var part in parts)
        {
            if (isAbsolute)
            {
                current = current == "/" ? $"/{part}" : $"{current}/{part}";
            }
            else
            {
                current = string.IsNullOrEmpty(current) ? part : $"{current}/{part}";
            }

            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }
}

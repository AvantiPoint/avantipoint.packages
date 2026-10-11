using System.Security.Cryptography;

namespace AvantiPoint.Packages.Registry.Native.Storage;

/// <summary>Bounded, seekable temporary upload; never buffers an artifact in RAM.</summary>
public sealed class ArtifactUpload : IAsyncDisposable
{
    private ArtifactUpload(FileStream stream, string sha256)
    {
        Stream = stream;
        Sha256 = sha256;
    }

    public FileStream Stream { get; }
    public string Sha256 { get; }
    public long Length => Stream.Length;

    public static async Task<ArtifactUpload> ReadAsync(Stream source, long limit, CancellationToken ct)
    {
        if (limit <= 0) throw new ArgumentOutOfRangeException(nameof(limit));
        var file = new FileStream(Path.Combine(Path.GetTempPath(), $"avp-native-{Guid.NewGuid():N}"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            int count;
            while ((count = await source.ReadAsync(buffer, ct)) != 0)
            {
                if (file.Length > limit - count) throw new ArtifactTooLargeException();
                hash.AppendData(buffer, 0, count);
                await file.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            file.Position = 0;
            return new(file, Convert.ToHexStringLower(hash.GetHashAndReset()));
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => Stream.DisposeAsync();
}

using System.Text;
using System.Security.Cryptography;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Storage.Tests.TestInfrastructure;

internal static class StorageRoundTrip
{
    public static async Task ExecuteAsync(IStorageService storage, CancellationToken cancellationToken = default)
    {
        const string path = "packages/roundtrip.test/1.0.0/roundtrip.test.1.0.0.nupkg";
        var payload = Encoding.UTF8.GetBytes("nupkg-roundtrip-test");
        await using var upload = new MemoryStream(payload);

        var putResult = await storage.PutAsync(
            path,
            upload,
            "application/octet-stream",
            cancellationToken);

        Assert.Equal(StoragePutResult.Success, putResult);

        await using var download = await storage.GetAsync(path, cancellationToken);
        using var reader = new StreamReader(download, Encoding.UTF8);
        var text = await reader.ReadToEndAsync(cancellationToken);
        Assert.Equal("nupkg-roundtrip-test", text);

        var found = false;
        await foreach (var file in storage.ListFilesAsync("packages/roundtrip.test", cancellationToken))
        {
            if (file.Path == path)
            {
                found = true;
                break;
            }
        }

        Assert.True(found);

        await storage.DeleteAsync(path, cancellationToken);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => storage.GetAsync(path, cancellationToken));

        await ExecuteStreamingAsync(storage, cancellationToken);
    }

    private static async Task ExecuteStreamingAsync(IStorageService storage, CancellationToken cancellationToken)
    {
        var streaming = Assert.IsAssignableFrom<IStreamingStorageService>(storage);
        var payload = new byte[1024 * 1024];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);
        var expected = SHA256.HashData(payload);
        var path = "native/v2/blobs/sha256/" + Convert.ToHexStringLower(expected) + "/data";
        await using var source = new MemoryStream(payload);
        await streaming.UploadAsync(path, source, "application/octet-stream", cancellationToken);
        Assert.True(source.CanRead); // Caller retains stream ownership.
        source.Position = 0;
        await streaming.UploadAsync(path, source, "application/octet-stream", cancellationToken);
        using var hash = SHA256.Create();
        await using var sink = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write, leaveOpen: true);
        await streaming.CopyToAsync(path, sink, cancellationToken);
        await sink.FlushFinalBlockAsync(cancellationToken);
        Assert.Equal(expected, hash.Hash);
        await storage.DeleteAsync(path, cancellationToken);
    }
}

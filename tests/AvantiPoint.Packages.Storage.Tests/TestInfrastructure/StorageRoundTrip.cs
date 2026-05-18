using System.Text;
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
    }
}

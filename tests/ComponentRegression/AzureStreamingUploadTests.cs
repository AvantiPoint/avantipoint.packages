using System.Security.Cryptography;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Blobs.Models;
using AvantiPoint.Packages.Registry.Oci.Tests.TestInfrastructure;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class AzureStreamingUploadTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Azure_blob_uploader_consumes_patch_and_completion_streams_asynchronously(bool completion)
    {
        var payload = new byte[5 * 1024 * 1024 + 137];
        new Random(42).NextBytes(payload);
        byte[] prefix = [11, 22, 33];
        byte[] expected = [.. prefix, .. payload];
        await using var existing = new AsyncOnlyReadStream(prefix);
        await using var requestBody = new AsyncOnlyReadStream(payload);
        await using var overlay = new OverlayReadStream(existing, requestBody, replacementOffset: null, leaveOpen: true);
        await using var tracked = new Sha256TrackingReadStream(overlay, leaveOpen: true);
        using var handler = new BlobUploadHandler();
        using var client = new HttpClient(handler);
        var options = new BlobClientOptions { Transport = new HttpClientTransport(client) };
        options.Retry.MaxRetries = 0;
        var container = new BlobContainerClient(new Uri("https://storage-fixture.invalid/packages"), options);
        var blob = container.GetBlockBlobClient("oci/docker/v2/uploads/synthetic/data");

        await blob.UploadAsync(completion ? tracked : overlay, new BlobUploadOptions
        {
            Metadata = new Dictionary<string, string> { ["ContentType"] = "application/octet-stream" }
        }, TestContext.Current.CancellationToken);

        Assert.Equal(expected, handler.UploadedContent);
        Assert.True(handler.Requests > 0);
        Assert.True(overlay.IsComplete);
        Assert.Equal(expected.Length, overlay.BytesRead);
        Assert.Equal(0, existing.SynchronousReads);
        Assert.Equal(0, requestBody.SynchronousReads);
        if (completion)
        {
            Assert.True(tracked.IsComplete);
            Assert.Equal(expected.Length, tracked.BytesRead);
            Assert.Equal("sha256:" + Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), tracked.GetDigest());
        }
    }
}

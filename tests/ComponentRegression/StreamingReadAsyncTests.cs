using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Packages.Registry.Oci.Tests.TestInfrastructure;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class StreamingReadAsyncTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_async_overloads_preserve_overlay_and_digest_without_synchronous_reads(bool memoryOverload)
    {
        await using var existing = new AsyncOnlyReadStream(Encoding.UTF8.GetBytes("before-after"));
        await using var replacement = new AsyncOnlyReadStream(Encoding.UTF8.GetBytes("new"));
        await using var overlay = new OverlayReadStream(existing, replacement, replacementOffset: 7, leaveOpen: true);
        await using var tracked = new Sha256TrackingReadStream(overlay, leaveOpen: true);
        var buffer = new byte[8];
        using var output = new MemoryStream();
        int read;
        while ((read = memoryOverload
            ? await tracked.ReadAsync(buffer.AsMemory(2, 3), TestContext.Current.CancellationToken)
            : await tracked.ReadAsync(buffer, 2, 3, TestContext.Current.CancellationToken)) != 0)
            await output.WriteAsync(buffer.AsMemory(2, read), TestContext.Current.CancellationToken);

        var expected = Encoding.UTF8.GetBytes("before-newer");
        Assert.Equal(expected, output.ToArray());
        Assert.Equal(expected.Length, overlay.BytesRead);
        Assert.Equal(expected.Length, tracked.BytesRead);
        Assert.Equal("sha256:" + Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(), tracked.GetDigest());
        Assert.True(overlay.IsComplete);
        Assert.True(tracked.IsComplete);
        Assert.Equal(0, existing.SynchronousReads);
        Assert.Equal(0, replacement.SynchronousReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Byte_array_async_overload_honors_cancellation(bool tracking)
    {
        await using var source = new AsyncOnlyReadStream([1, 2, 3]);
        await using var overlay = new OverlayReadStream(Stream.Null, source, replacementOffset: null, leaveOpen: true);
        await using var tracked = new Sha256TrackingReadStream(overlay, leaveOpen: true);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Stream stream = tracking ? tracked : overlay;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stream.ReadAsync(new byte[8], 1, 4, cancellation.Token));
        Assert.Equal(0, source.SynchronousReads);
        Assert.Equal(0, overlay.BytesRead);
        Assert.Equal(0, tracked.BytesRead);
    }
}

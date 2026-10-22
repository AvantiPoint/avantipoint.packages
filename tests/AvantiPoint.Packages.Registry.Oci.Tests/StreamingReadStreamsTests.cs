using System.Security.Cryptography;
using System.Text;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class StreamingReadStreamsTests
{
    [Fact]
    public async Task OverlayReadStream_AppendsWithoutSeekingOrBufferingTheInputs()
    {
        await using var existing = new NonSeekableReadStream(Encoding.UTF8.GetBytes("existing-"));
        await using var replacement = new NonSeekableReadStream(Encoding.UTF8.GetBytes("chunk"));
        await using var overlay = new OverlayReadStream(existing, replacement, replacementOffset: null, leaveOpen: true);
        using var output = new MemoryStream();

        await overlay.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("existing-chunk", Encoding.UTF8.GetString(output.ToArray()));
        Assert.Equal(output.Length, overlay.BytesRead);
        Assert.True(overlay.IsComplete);
    }

    [Fact]
    public async Task OverlayReadStream_OverwritesAtOffsetAndRetainsTheSuffix()
    {
        await using var existing = new NonSeekableReadStream(Encoding.UTF8.GetBytes("abcdef"));
        await using var replacement = new NonSeekableReadStream(Encoding.UTF8.GetBytes("XYZ"));
        await using var overlay = new OverlayReadStream(existing, replacement, replacementOffset: 2, leaveOpen: true);
        using var output = new MemoryStream();

        await overlay.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal("abXYZf", Encoding.UTF8.GetString(output.ToArray()));
        Assert.True(overlay.IsComplete);
    }

    [Fact]
    public async Task OverlayReadStream_ZeroFillsAnOffsetBeyondTheExistingUpload()
    {
        await using var existing = new NonSeekableReadStream(Encoding.UTF8.GetBytes("ab"));
        await using var replacement = new NonSeekableReadStream(Encoding.UTF8.GetBytes("z"));
        await using var overlay = new OverlayReadStream(existing, replacement, replacementOffset: 4, leaveOpen: true);
        using var output = new MemoryStream();

        await overlay.CopyToAsync(output, TestContext.Current.CancellationToken);

        Assert.Equal([.. Encoding.UTF8.GetBytes("ab"), 0, 0, (byte)'z'], output.ToArray());
        Assert.True(overlay.IsComplete);
    }

    [Fact]
    public async Task Sha256TrackingReadStream_HashesAForwardOnlySourceAsItIsConsumed()
    {
        var bytes = Encoding.UTF8.GetBytes("streamed OCI layer content");
        await using var source = new NonSeekableReadStream(bytes);
        await using var tracked = new Sha256TrackingReadStream(source, leaveOpen: true);
        using var output = new MemoryStream();

        await tracked.CopyToAsync(output, TestContext.Current.CancellationToken);

        var expected = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        Assert.Equal(expected, tracked.GetDigest());
        Assert.Equal(bytes.Length, tracked.BytesRead);
        Assert.True(tracked.IsComplete);
    }

    private sealed class NonSeekableReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content, writable: false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => _inner.Read(buffer);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        public override ValueTask DisposeAsync() => _inner.DisposeAsync();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

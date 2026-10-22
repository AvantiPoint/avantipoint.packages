namespace AvantiPoint.Packages.Registry.Native.Storage;

/// <summary>Bounds the complete multipart request, including chunked bodies.</summary>
internal sealed class ArtifactRequestBodyStream(Stream source, long limit) : Stream
{
    private long _read;
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => _read; set => throw new NotSupportedException(); }
    private int Count(int count)
    {
        if (_read > limit - count) throw new ArtifactTooLargeException();
        _read += count;
        return count;
    }
    public override int Read(byte[] buffer, int offset, int count) => Count(source.Read(buffer, offset, count));
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        Count(await source.ReadAsync(buffer, cancellationToken));
    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        Count(await source.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

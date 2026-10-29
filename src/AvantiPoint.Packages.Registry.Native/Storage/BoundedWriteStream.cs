namespace AvantiPoint.Packages.Registry.Native.Storage;

/// <summary>Enforces catalog length on streaming storage transfers without owning the destination.</summary>
internal sealed class BoundedWriteStream(Stream destination, long limit, CancellationToken cancellationToken) : Stream
{
    public long BytesWritten { get; private set; }
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => BytesWritten;
    public override long Position { get => BytesWritten; set => throw new NotSupportedException(); }
    public override void Flush() => destination.Flush();
    public override Task FlushAsync(CancellationToken ct) => destination.FlushAsync(ct);
    public override void Write(byte[] buffer, int offset, int count)
    {
        Validate(count);
        destination.Write(buffer, offset, count);
        BytesWritten += count;
    }
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Validate(buffer.Length);
        destination.Write(buffer);
        BytesWritten += buffer.Length;
    }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
        WriteAsync(buffer.AsMemory(offset, count), ct).AsTask();
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        Validate(buffer.Length);
        await destination.WriteAsync(buffer, ct);
        BytesWritten += buffer.Length;
    }
    private void Validate(int count)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count > limit - BytesWritten) throw new IOException("Stored artifact exceeds its catalog length.");
    }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}

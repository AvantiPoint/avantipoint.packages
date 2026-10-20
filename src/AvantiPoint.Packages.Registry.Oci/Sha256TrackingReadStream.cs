using System.Security.Cryptography;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Computes a SHA-256 digest and byte count as a forward-only stream is consumed.
/// </summary>
internal sealed class Sha256TrackingReadStream : Stream
{
    private readonly Stream _source;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly bool _leaveOpen;
    private bool _complete;
    private bool _disposed;

    public Sha256TrackingReadStream(Stream source, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
        _leaveOpen = leaveOpen;
    }

    public long BytesRead { get; private set; }

    public bool IsComplete => _complete;

    public string GetDigest()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_complete)
        {
            throw new InvalidOperationException("The source must be consumed before its digest is available.");
        }

        return $"sha256:{Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ValidateBuffer(buffer, offset, count);
        var read = _source.Read(buffer, offset, count);
        Track(buffer.AsSpan(offset, read), read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var read = _source.Read(buffer);
        Track(buffer[..read], read);
        return read;
    }

    // Azure's uploader uses this overload; Stream's default falls back to synchronous Read.
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ValidateBuffer(buffer, offset, count);
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var read = await _source.ReadAsync(buffer, cancellationToken);
        Track(buffer.Span[..read], read);
        return read;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _hash.Dispose();
            if (!_leaveOpen)
            {
                _source.Dispose();
            }
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _hash.Dispose();
            if (!_leaveOpen)
            {
                await _source.DisposeAsync();
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private void Track(ReadOnlySpan<byte> buffer, int count)
    {
        if (count == 0)
        {
            _complete = true;
            return;
        }

        _hash.AppendData(buffer);
        BytesRead += count;
    }

    private static void ValidateBuffer(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count)
        {
            throw new ArgumentException("Offset and count exceed the buffer length.");
        }
    }
}

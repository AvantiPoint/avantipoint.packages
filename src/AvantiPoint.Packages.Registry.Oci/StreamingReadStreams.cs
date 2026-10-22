using System.Security.Cryptography;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Presents an existing upload with a replacement stream overlaid at an optional
/// offset. When no offset is supplied, the replacement is appended. The stream is
/// deliberately forward-only so upload processing never depends on materializing
/// the complete blob or seeking the request body.
/// </summary>
internal sealed class OverlayReadStream : Stream
{
    private const int SkipBufferSize = 81_920;

    private readonly Stream _existing;
    private readonly Stream _replacement;
    private readonly long? _replacementOffset;
    private readonly bool _leaveOpen;
    private readonly byte[] _skipBuffer = new byte[SkipBufferSize];
    private Phase _phase = Phase.Prefix;
    private long _prefixBytesProduced;
    private bool _existingEnded;
    private bool _disposed;

    public OverlayReadStream(
        Stream existing,
        Stream replacement,
        long? replacementOffset,
        bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacementOffset is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replacementOffset));
        }

        _existing = existing;
        _replacement = replacement;
        _replacementOffset = replacementOffset;
        _leaveOpen = leaveOpen;
    }

    public long BytesRead { get; private set; }

    public bool IsComplete => _phase == Phase.Complete;

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
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (true)
        {
            switch (_phase)
            {
                case Phase.Prefix:
                {
                    var count = ReadPrefix(buffer);
                    if (count > 0)
                    {
                        return Record(count);
                    }

                    _phase = Phase.Replacement;
                    continue;
                }
                case Phase.Replacement:
                {
                    var count = _replacement.Read(buffer);
                    if (count > 0)
                    {
                        SkipExisting(count);
                        return Record(count);
                    }

                    _phase = _replacementOffset.HasValue ? Phase.Suffix : Phase.Complete;
                    continue;
                }
                case Phase.Suffix:
                {
                    var count = _existingEnded ? 0 : _existing.Read(buffer);
                    if (count > 0)
                    {
                        return Record(count);
                    }

                    _existingEnded = true;
                    _phase = Phase.Complete;
                    continue;
                }
                default:
                    return 0;
            }
        }
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (buffer.IsEmpty)
        {
            return 0;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (_phase)
            {
                case Phase.Prefix:
                {
                    var count = await ReadPrefixAsync(buffer, cancellationToken);
                    if (count > 0)
                    {
                        return Record(count);
                    }

                    _phase = Phase.Replacement;
                    continue;
                }
                case Phase.Replacement:
                {
                    var count = await _replacement.ReadAsync(buffer, cancellationToken);
                    if (count > 0)
                    {
                        await SkipExistingAsync(count, cancellationToken);
                        return Record(count);
                    }

                    _phase = _replacementOffset.HasValue ? Phase.Suffix : Phase.Complete;
                    continue;
                }
                case Phase.Suffix:
                {
                    var count = _existingEnded
                        ? 0
                        : await _existing.ReadAsync(buffer, cancellationToken);
                    if (count > 0)
                    {
                        return Record(count);
                    }

                    _existingEnded = true;
                    _phase = Phase.Complete;
                    continue;
                }
                default:
                    return 0;
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing && !_leaveOpen)
        {
            _replacement.Dispose();
            _existing.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed && !_leaveOpen)
        {
            await _replacement.DisposeAsync();
            await _existing.DisposeAsync();
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

    private int ReadPrefix(Span<byte> buffer)
    {
        if (!_replacementOffset.HasValue)
        {
            var readCount = _existingEnded ? 0 : _existing.Read(buffer);
            if (readCount == 0)
            {
                _existingEnded = true;
            }

            return readCount;
        }

        var remaining = _replacementOffset.Value - _prefixBytesProduced;
        if (remaining <= 0)
        {
            return 0;
        }

        var requested = (int)Math.Min(buffer.Length, remaining);
        var count = _existingEnded ? 0 : _existing.Read(buffer[..requested]);
        if (count == 0)
        {
            _existingEnded = true;
            buffer[..requested].Clear();
            count = requested;
        }

        _prefixBytesProduced += count;
        return count;
    }

    private async ValueTask<int> ReadPrefixAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (!_replacementOffset.HasValue)
        {
            var readCount = _existingEnded
                ? 0
                : await _existing.ReadAsync(buffer, cancellationToken);
            if (readCount == 0)
            {
                _existingEnded = true;
            }

            return readCount;
        }

        var remaining = _replacementOffset.Value - _prefixBytesProduced;
        if (remaining <= 0)
        {
            return 0;
        }

        var requested = (int)Math.Min(buffer.Length, remaining);
        var count = _existingEnded
            ? 0
            : await _existing.ReadAsync(buffer[..requested], cancellationToken);
        if (count == 0)
        {
            _existingEnded = true;
            buffer.Span[..requested].Clear();
            count = requested;
        }

        _prefixBytesProduced += count;
        return count;
    }

    private void SkipExisting(int count)
    {
        while (!_existingEnded && count > 0)
        {
            var read = _existing.Read(_skipBuffer, 0, Math.Min(count, _skipBuffer.Length));
            if (read == 0)
            {
                _existingEnded = true;
                return;
            }

            count -= read;
        }
    }

    private async ValueTask SkipExistingAsync(int count, CancellationToken cancellationToken)
    {
        while (!_existingEnded && count > 0)
        {
            var read = await _existing.ReadAsync(
                _skipBuffer.AsMemory(0, Math.Min(count, _skipBuffer.Length)),
                cancellationToken);
            if (read == 0)
            {
                _existingEnded = true;
                return;
            }

            count -= read;
        }
    }

    private int Record(int count)
    {
        BytesRead += count;
        return count;
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

    private enum Phase
    {
        Prefix,
        Replacement,
        Suffix,
        Complete,
    }
}

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

/// <summary>
/// Keeps an HTTP response alive for as long as its content stream is in use.
/// </summary>
internal sealed class HttpResponseOwnedReadStream : Stream
{
    private readonly Stream _content;
    private HttpResponseMessage? _response;

    public HttpResponseOwnedReadStream(Stream content, HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(response);
        _content = content;
        _response = response;
    }

    public override bool CanRead => _content.CanRead;

    public override bool CanSeek => _content.CanSeek;

    public override bool CanWrite => false;

    public override bool CanTimeout => _content.CanTimeout;

    public override int ReadTimeout
    {
        get => _content.ReadTimeout;
        set => _content.ReadTimeout = value;
    }

    public override long Length => _content.Length;

    public override long Position
    {
        get => _content.Position;
        set => _content.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        _content.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => _content.Read(buffer);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        _content.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _content.Seek(offset, origin);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Interlocked.Exchange(ref _response, null)?.Dispose();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        var response = Interlocked.Exchange(ref _response, null);
        if (response is not null)
        {
            await _content.DisposeAsync();
            response.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    public override void Flush() => throw new NotSupportedException();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        Task.FromException(new NotSupportedException());

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

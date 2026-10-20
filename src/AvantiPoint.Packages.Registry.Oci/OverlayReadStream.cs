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

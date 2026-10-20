namespace AvantiPoint.Packages.Registry.Oci;

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

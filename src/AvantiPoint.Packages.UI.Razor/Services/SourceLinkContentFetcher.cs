using System.Net;
using System.Net.Sockets;
using System.Text;

namespace AvantiPoint.Packages.UI.Services;

internal interface ISourceLinkHostResolver
{
    ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken);
}

internal sealed class SourceLinkContentFetcher : ISourceLinkContentFetcher, IDisposable
{
    internal const int DefaultMaximumResponseBytes = 2 * 1024 * 1024;
    internal const int DefaultMaximumRedirects = 5;
    internal static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(6);

    private readonly HttpMessageInvoker _http;
    private readonly ISourceLinkHostResolver _resolver;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maximumResponseBytes;
    private readonly int _maximumRedirects;
    private readonly bool _disposeHttp;

    public SourceLinkContentFetcher()
        : this(
            CreateHardenedTransport(),
            SystemSourceLinkHostResolver.Instance,
            DefaultRequestTimeout,
            DefaultMaximumResponseBytes,
            DefaultMaximumRedirects,
            disposeHttp: true)
    {
    }

    internal SourceLinkContentFetcher(
        HttpMessageInvoker http,
        ISourceLinkHostResolver resolver,
        TimeSpan? requestTimeout = null,
        int maximumResponseBytes = DefaultMaximumResponseBytes,
        int maximumRedirects = DefaultMaximumRedirects,
        bool disposeHttp = false)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumRedirects);
        if (requestTimeout is { } configuredTimeout && configuredTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }

        _http = http;
        _resolver = resolver;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
        _maximumResponseBytes = maximumResponseBytes;
        _maximumRedirects = maximumRedirects;
        _disposeHttp = disposeHttp;
    }

    public async Task<string?> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);

        try
        {
            return await FetchCoreAsync(uri, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposeHttp)
        {
            _http.Dispose();
        }
    }

    internal static bool IsGloballyRoutable(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            var first = bytes[0];
            var second = bytes[1];

            return first != 0
                   && first != 10
                   && first != 127
                   && !(first == 100 && second is >= 64 and <= 127)
                   && !(first == 169 && second == 254)
                   && !(first == 172 && second is >= 16 and <= 31)
                   && !(first == 192 && second == 0 && bytes[2] == 0)
                   && !(first == 192 && second == 0 && bytes[2] == 2)
                   && !(first == 192 && second == 88 && bytes[2] == 99)
                   && !(first == 192 && second == 168)
                   && !(first == 198 && second is 18 or 19)
                   && !(first == 198 && second == 51 && bytes[2] == 100)
                   && !(first == 203 && second == 0 && bytes[2] == 113)
                   && first < 224;
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6
            || IPAddress.IsLoopback(address)
            || address.IsIPv6LinkLocal
            || address.IsIPv6Multicast
            || address.IsIPv6SiteLocal)
        {
            return false;
        }

        var ipv6 = address.GetAddressBytes();
        var isGlobalUnicast = (ipv6[0] & 0xe0) == 0x20;
        var isDocumentation = ipv6[0] == 0x20
                              && ipv6[1] == 0x01
                              && ipv6[2] == 0x0d
                              && ipv6[3] == 0xb8;
        var isTeredo = ipv6[0] == 0x20
                       && ipv6[1] == 0x01
                       && ipv6[2] == 0
                       && ipv6[3] == 0;
        var isSixToFour = ipv6[0] == 0x20 && ipv6[1] == 0x02;

        return isGlobalUnicast && !isDocumentation && !isTeredo && !isSixToFour;
    }

    private async Task<string?> FetchCoreAsync(Uri initialUri, CancellationToken cancellationToken)
    {
        var currentUri = initialUri;
        for (var redirects = 0; ; redirects++)
        {
            if (!await IsSafeDestinationAsync(currentUri, cancellationToken))
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            request.Headers.UserAgent.ParseAdd("AvantiPoint.Packages.SourceLink/1.0");
            request.Headers.Accept.ParseAdd("text/plain, text/*;q=0.9, */*;q=0.1");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (IsRedirect(response.StatusCode))
            {
                if (redirects >= _maximumRedirects
                    || response.Headers.Location is not { } location
                    || !TryResolveRedirect(currentUri, location, out var redirectUri)
                    || IsHttpsDowngrade(currentUri, redirectUri))
                {
                    return null;
                }

                currentUri = redirectUri;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await ReadBoundedTextAsync(response.Content, cancellationToken);
        }
    }

    private async ValueTask<bool> IsSafeDestinationAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!uri.IsAbsoluteUri
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrWhiteSpace(uri.IdnHost)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.IsLoopback
            || uri.Port is <= 0 or > 65535)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.Contains('%'))
        {
            return false;
        }

        IReadOnlyList<IPAddress> addresses;
        if (IPAddress.TryParse(host, out var literal))
        {
            addresses = [literal];
        }
        else
        {
            addresses = await _resolver.ResolveAsync(host, cancellationToken);
        }

        return addresses.Count > 0 && addresses.All(IsGloballyRoutable);
    }

    private async Task<string?> ReadBoundedTextAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is { } declaredLength && declaredLength > _maximumResponseBytes)
        {
            return null;
        }

        await using var contentStream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream(Math.Min(_maximumResponseBytes, 64 * 1024));
        var chunk = new byte[Math.Min(16 * 1024, _maximumResponseBytes + 1)];
        while (true)
        {
            var remaining = _maximumResponseBytes - checked((int)buffer.Length);
            var read = await contentStream.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, remaining + 1)),
                cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (read > remaining)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        buffer.Position = 0;
        var encoding = ResolveEncoding(content.Headers.ContentType?.CharSet);
        using var reader = new StreamReader(buffer, encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (!string.IsNullOrWhiteSpace(charset))
        {
            try
            {
                return Encoding.GetEncoding(charset.Trim('"', '\''));
            }
            catch (ArgumentException)
            {
                // Treat an invalid or unsupported declared charset as UTF-8.
            }
        }

        return Encoding.UTF8;
    }

    private static bool IsRedirect(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static bool TryResolveRedirect(Uri currentUri, Uri location, out Uri redirectUri)
    {
        try
        {
            redirectUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
            return true;
        }
        catch (UriFormatException)
        {
            redirectUri = null!;
            return false;
        }
    }

    private static bool IsHttpsDowngrade(Uri currentUri, Uri redirectUri)
        => currentUri.Scheme == Uri.UriSchemeHttps && redirectUri.Scheme != Uri.UriSchemeHttps;

    private static HttpMessageInvoker CreateHardenedTransport()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxResponseHeadersLength = 32,
            ConnectCallback = ConnectToValidatedAddressAsync
        };
        return new HttpMessageInvoker(handler, disposeHandler: true);
    }

    private static async ValueTask<Stream> ConnectToValidatedAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await SystemSourceLinkHostResolver.Instance.ResolveAsync(
            context.DnsEndPoint.Host,
            cancellationToken);
        if (addresses.Count == 0 || addresses.Any(static address => !IsGloballyRoutable(address)))
        {
            throw new HttpRequestException("SourceLink destination did not resolve exclusively to public addresses.");
        }

        Exception? lastError = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is SocketException or IOException)
            {
                lastError = ex;
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException("Could not connect to a validated SourceLink destination.", lastError);
    }

    private sealed class SystemSourceLinkHostResolver : ISourceLinkHostResolver
    {
        public static SystemSourceLinkHostResolver Instance { get; } = new();

        public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken)
            => await Dns.GetHostAddressesAsync(host, cancellationToken);
    }
}

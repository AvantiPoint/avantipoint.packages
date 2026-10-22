using System.Net;
using System.Net.Http.Headers;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class SourceLinkContentFetcherTests
{
    private static readonly IPAddress PublicAddress = IPAddress.Parse("93.184.216.34");

    [Fact]
    public async Task FetchAsync_ReturnsBoundedTextFromPublicDestination()
    {
        var handler = new RecordingHandler(static (_, _) => Response(HttpStatusCode.OK, "public source"));
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(PublicAddress));

        var text = await fetcher.FetchAsync(new Uri("https://source.example/source.cs"), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("public source", text);
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal("source.example", Assert.Single(handler.RequestUris).Host);
    }

    [Theory]
    [InlineData("http://127.0.0.1/source.cs")]
    [InlineData("http://localhost/source.cs")]
    [InlineData("http://10.12.0.3/source.cs")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://192.168.1.2/source.cs")]
    [InlineData("http://[::1]/source.cs")]
    [InlineData("http://[fe80::1]/source.cs")]
    [InlineData("http://[fc00::1]/source.cs")]
    [InlineData("ftp://93.184.216.34/source.cs")]
    [InlineData("https://user:password@source.example/source.cs")]
    public async Task FetchAsync_RejectsUnsafeLiteralOrUriBeforeSending(string value)
    {
        var handler = new RecordingHandler(static (_, _) => Response(HttpStatusCode.OK, "should not be read"));
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(PublicAddress));

        var text = await fetcher.FetchAsync(new Uri(value), Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_RejectsHostResolvingToPrivateAddress()
    {
        var handler = new RecordingHandler(static (_, _) => Response(HttpStatusCode.OK, "should not be read"));
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(IPAddress.Parse("10.0.0.8")));

        var text = await fetcher.FetchAsync(new Uri("https://source.example/source.cs"), Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_RejectsMixedPublicAndPrivateDnsAnswers()
    {
        var handler = new RecordingHandler(static (_, _) => Response(HttpStatusCode.OK, "should not be read"));
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(PublicAddress, IPAddress.Loopback));

        var text = await fetcher.FetchAsync(new Uri("https://source.example/source.cs"), Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_RevalidatesRedirectAndRejectsPrivateTarget()
    {
        var handler = new RecordingHandler(static (_, _) => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("http://127.0.0.1/secrets") }
        });
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(PublicAddress));

        var text = await fetcher.FetchAsync(new Uri("https://source.example/source.cs"), Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_FollowsRelativeRedirectBetweenValidatedPublicDestinations()
    {
        var handler = new RecordingHandler(static (request, _) =>
            request.RequestUri!.AbsolutePath == "/first"
                ? new HttpResponseMessage(HttpStatusCode.Redirect)
                {
                    Headers = { Location = new Uri("/source.cs", UriKind.Relative) }
                }
                : Response(HttpStatusCode.OK, "redirected source"));
        using var http = new HttpMessageInvoker(handler);
        var resolver = new StaticResolver(PublicAddress);
        using var fetcher = Create(http, resolver);

        var text = await fetcher.FetchAsync(new Uri("https://source.example/first"), Xunit.TestContext.Current.CancellationToken);

        Assert.Equal("redirected source", text);
        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(2, resolver.CallCount);
    }

    [Fact]
    public async Task FetchAsync_RejectsHttpsToHttpDowngrade()
    {
        var handler = new RecordingHandler(static (_, _) => new HttpResponseMessage(HttpStatusCode.Redirect)
        {
            Headers = { Location = new Uri("http://source.example/source.cs") }
        });
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(PublicAddress));

        var text = await fetcher.FetchAsync(new Uri("https://source.example/first"), Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_RejectsStreamingResponseBeyondByteLimit()
    {
        var content = new UnknownLengthContent(new byte[65]);
        var handler = new RecordingHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        });
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, new StaticResolver(PublicAddress), maximumResponseBytes: 64);

        var text = await fetcher.FetchAsync(new Uri("https://source.example/source.cs"), Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_PropagatesCallerCancellation()
    {
        var resolver = new BlockingResolver();
        var handler = new RecordingHandler(static (_, _) => Response(HttpStatusCode.OK, "should not be read"));
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = Create(http, resolver);
        using var cancellation = new CancellationTokenSource();

        var fetch = fetcher.FetchAsync(new Uri("https://source.example/source.cs"), cancellation.Token);
        await resolver.Started.Task.WaitAsync(Xunit.TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fetch);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task FetchAsync_UsesIndependentShortTimeoutAsSoftFailure()
    {
        var resolver = new BlockingResolver();
        var handler = new RecordingHandler(static (_, _) => Response(HttpStatusCode.OK, "should not be read"));
        using var http = new HttpMessageInvoker(handler);
        using var fetcher = new SourceLinkContentFetcher(
            http,
            resolver,
            requestTimeout: TimeSpan.FromMilliseconds(50));

        var text = await fetcher.FetchAsync(
            new Uri("https://source.example/source.cs"),
            Xunit.TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Equal(0, handler.RequestCount);
        Assert.InRange(
            SourceLinkContentFetcher.DefaultRequestTimeout,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData("93.184.216.34", true)]
    [InlineData("8.8.8.8", true)]
    [InlineData("0.0.0.0", false)]
    [InlineData("127.12.0.1", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("169.254.8.8", false)]
    [InlineData("198.18.0.1", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("::", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fc00::1", false)]
    [InlineData("ff02::1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2002:7f00:1::", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    public void IsGloballyRoutable_ClassifiesSpecialRanges(string value, bool expected)
    {
        Assert.Equal(expected, SourceLinkContentFetcher.IsGloballyRoutable(IPAddress.Parse(value)));
    }

    private static SourceLinkContentFetcher Create(
        HttpMessageInvoker http,
        ISourceLinkHostResolver resolver,
        int maximumResponseBytes = SourceLinkContentFetcher.DefaultMaximumResponseBytes)
        => new(
            http,
            resolver,
            requestTimeout: TimeSpan.FromSeconds(2),
            maximumResponseBytes: maximumResponseBytes);

    private static HttpResponseMessage Response(HttpStatusCode statusCode, string text)
        => new(statusCode)
        {
            Content = new StringContent(text)
        };

    private sealed class StaticResolver(params IPAddress[] addresses) : ISourceLinkHostResolver
    {
        public int CallCount { get; private set; }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            CallCount++;
            return ValueTask.FromResult<IReadOnlyList<IPAddress>>(addresses);
        }
    }

    private sealed class BlockingResolver : ISourceLinkHostResolver
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(
            string host,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Array.Empty<IPAddress>();
        }
    }

    private sealed class RecordingHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(responder(request, cancellationToken));
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}

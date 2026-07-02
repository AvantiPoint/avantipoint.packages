using System.Net;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Npm.Tests;

public sealed class NpmMirrorServiceTests
{
    [Fact]
    public async Task FetchPackumentAsync_SendsBearerToken_ForConfiguredRegistry()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureRequestHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"name":"@fortawesome/fontawesome-pro"}""");
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions { Url = "https://npm.fontawesome.com", Token = "fa-token" },
        ]);

        var packument = await service.FetchPackumentAsync(
            "@fortawesome/fontawesome-pro",
            TestContext.Current.CancellationToken);

        Assert.NotNull(packument);
        Assert.NotNull(captured);
        Assert.Equal("Bearer", captured!.Headers.Authorization?.Scheme);
        Assert.Equal("fa-token", captured.Headers.Authorization?.Parameter);
        Assert.StartsWith("https://npm.fontawesome.com/", captured.RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchPackumentAsync_SendsBasicCredentials_WhenNoToken()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureRequestHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"name":"left-pad"}""");
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions
            {
                Url = "https://npm.example.com",
                Username = "svc",
                Password = "s3cret",
            },
        ]);

        var packument = await service.FetchPackumentAsync("left-pad", TestContext.Current.CancellationToken);

        Assert.NotNull(packument);
        Assert.Equal("Basic", captured!.Headers.Authorization?.Scheme);
        var expected = Convert.ToBase64String(Encoding.UTF8.GetBytes("svc:s3cret"));
        Assert.Equal(expected, captured.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task FetchPackumentAsync_FallsBackToNextRegistry_InPriorityOrder()
    {
        var requestedHosts = new List<string>();
        var handler = new CaptureRequestHandler(request =>
        {
            requestedHosts.Add(request.RequestUri!.Host);
            return request.RequestUri.Host == "primary.example.com"
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : JsonResponse("""{"name":"left-pad"}""");
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions { Url = "https://fallback.example.com", Priority = 10 },
            new NpmUpstreamRegistryOptions { Url = "https://primary.example.com", Priority = 0 },
        ]);

        var packument = await service.FetchPackumentAsync("left-pad", TestContext.Current.CancellationToken);

        Assert.NotNull(packument);
        Assert.Equal(["primary.example.com", "fallback.example.com"], requestedHosts);
    }

    [Fact]
    public async Task FetchPackumentAsync_UsesRegistryUrl_WhenNoRegistriesConfigured()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureRequestHandler(request =>
        {
            captured = request;
            return JsonResponse("""{"name":"left-pad"}""");
        });

        var service = CreateService(handler, registryUrl: "https://registry.npmjs.org");

        var packument = await service.FetchPackumentAsync("left-pad", TestContext.Current.CancellationToken);

        Assert.NotNull(packument);
        Assert.Equal("registry.npmjs.org", captured!.RequestUri!.Host);
        Assert.Null(captured.Headers.Authorization);
    }

    [Fact]
    public async Task FetchTarballAsync_AttachesCredentials_ForMatchingHostOnly()
    {
        var authorizations = new Dictionary<string, string?>();
        var handler = new CaptureRequestHandler(request =>
        {
            authorizations[request.RequestUri!.Host] = request.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            };
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions { Url = "https://npm.fontawesome.com", Token = "fa-token" },
        ]);

        await using var matching = await service.FetchTarballAsync(
            "https://npm.fontawesome.com/@fortawesome/pro/-/pro-6.0.0.tgz",
            TestContext.Current.CancellationToken);
        await using var other = await service.FetchTarballAsync(
            "https://registry.npmjs.org/left-pad/-/left-pad-1.3.0.tgz",
            TestContext.Current.CancellationToken);

        Assert.NotNull(matching);
        Assert.NotNull(other);
        Assert.Equal("fa-token", authorizations["npm.fontawesome.com"]);
        Assert.Null(authorizations["registry.npmjs.org"]);
    }

    [Fact]
    public async Task FetchTarballAsync_SelectsRegistryByLongestPathPrefix_WhenHostsCollide()
    {
        var authorizations = new List<string?>();
        var handler = new CaptureRequestHandler(request =>
        {
            authorizations.Add(request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3]),
            };
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions
            {
                Url = "https://pkgs.dev.azure.com/org/feed-a/_packaging/a/npm/registry",
                Token = "token-a",
                Priority = 0,
            },
            new NpmUpstreamRegistryOptions
            {
                Url = "https://pkgs.dev.azure.com/org/feed-b/_packaging/b/npm/registry",
                Token = "token-b",
                Priority = 10,
            },
        ]);

        await using var tarball = await service.FetchTarballAsync(
            "https://pkgs.dev.azure.com/org/feed-b/_packaging/b/npm/registry/left-pad/-/left-pad-1.3.0.tgz",
            TestContext.Current.CancellationToken);

        Assert.NotNull(tarball);
        Assert.Equal(["token-b"], authorizations);
    }

    [Fact]
    public async Task FetchPackumentAsync_FallsBack_WhenUpstreamTimesOut()
    {
        var requestedHosts = new List<string>();
        var handler = new CaptureRequestHandler(request =>
        {
            requestedHosts.Add(request.RequestUri!.Host);
            if (request.RequestUri.Host == "slow.example.com")
            {
                // HttpClient surfaces its own timeout as a cancellation even though the
                // caller's token is not canceled.
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");
            }

            return JsonResponse("""{"name":"left-pad"}""");
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions { Url = "https://slow.example.com", Priority = 0 },
            new NpmUpstreamRegistryOptions { Url = "https://fallback.example.com", Priority = 10 },
        ]);

        var packument = await service.FetchPackumentAsync("left-pad", TestContext.Current.CancellationToken);

        Assert.NotNull(packument);
        Assert.Equal(["slow.example.com", "fallback.example.com"], requestedHosts);
    }

    [Fact]
    public async Task FetchPackumentAsync_Throws_WhenCallerCancels()
    {
        using var cts = new CancellationTokenSource();
        var handler = new CaptureRequestHandler(_ =>
        {
            cts.Cancel();
            throw new TaskCanceledException("canceled");
        });

        var service = CreateService(handler, registries:
        [
            new NpmUpstreamRegistryOptions { Url = "https://primary.example.com", Priority = 0 },
            new NpmUpstreamRegistryOptions { Url = "https://fallback.example.com", Priority = 10 },
        ]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.FetchPackumentAsync("left-pad", cts.Token));
    }

    private static NpmMirrorService CreateService(
        HttpMessageHandler handler,
        IEnumerable<NpmUpstreamRegistryOptions>? registries = null,
        string? registryUrl = null)
    {
        var options = new NpmFeedOptions
        {
            Mirror = new NpmMirrorOptions
            {
                Registries = registries?.ToList() ?? [],
            },
        };

        if (registryUrl is not null)
        {
            options.Mirror.RegistryUrl = registryUrl;
        }

        return new NpmMirrorService(
            new ConfigurationNpmUpstreamRegistryProvider(Options.Create(options)),
            new FixedMirrorPolicyService(),
            new SharedHandlerHttpClientFactory(handler),
            NullLogger<NpmMirrorService>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class FixedMirrorPolicyService : IMirrorPolicyService
    {
        public MirrorCachingStrategy GetStrategy(FeedProtocol protocol, string? surfaceId = null) =>
            MirrorCachingStrategy.IndexAndCache;

        public bool IncludeInDiscovery(FeedProtocol protocol, PackageOrigin origin, string? surfaceId = null) => true;
    }

    private sealed class CaptureRequestHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class SharedHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

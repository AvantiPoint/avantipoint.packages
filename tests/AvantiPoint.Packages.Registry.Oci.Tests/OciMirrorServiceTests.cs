using System.Net;
using System.Net.Http.Headers;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Registry.Oci;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciMirrorServiceTests
{
    [Fact]
    public async Task TryFetchManifestAsync_AdvertisesIndexMediaTypes()
    {
        HttpRequestMessage? captured = null;
        var handler = new CaptureRequestHandler(request =>
        {
            captured = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Headers = { { "Docker-Content-Digest", "sha256:abc" } },
                Content = new StringContent(
                    "{}",
                    new MediaTypeHeaderValue("application/vnd.oci.image.index.v1+json")),
            };
        });

        var options = new OciFeedOptions
        {
            Mirror = new OciMirrorOptions
            {
                Registries =
                [
                    new OciUpstreamRegistryOptions { Url = "https://registry.example", Priority = 0 },
                ],
            },
        };

        var accessor = new OciFeedOptionsAccessor(new TestOptionsMonitor<OciFeedOptions>(options));
        var service = new OciMirrorService(
            accessor,
            new DefaultMirrorPolicyService(),
            new SingleClientHttpClientFactory(new HttpClient(handler)),
            NullLogger<OciMirrorService>.Instance);

        var surface = new SurfaceContext(
            "default",
            FeedProtocol.Oci,
            "default",
            null,
            "/v2",
            new Uri("https://feed.example/v2/"));

        var result = await service.TryFetchManifestAsync(
            surface,
            "library/nginx",
            "latest",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.NotNull(captured);
        var accept = captured!.Headers.Accept.Select(h => h.MediaType).ToList();
        Assert.Contains("application/vnd.oci.image.index.v1+json", accept);
        Assert.Contains("application/vnd.docker.distribution.manifest.list.v2+json", accept);
    }

    private sealed class CaptureRequestHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class SingleClientHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

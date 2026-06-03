using System.Net;
using System.Net.Http.Headers;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
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

        var service = CreateService(handler);
        var surface = CreateSurface();

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

    [Fact]
    public async Task TryFetchManifestAsync_FollowsBearerChallengeAndRetriesWithToken()
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new CaptureRequestHandler(request =>
        {
            requests.Add(CloneRequest(request));
            if (request.RequestUri?.AbsolutePath == "/v2/library/nginx/manifests/latest")
            {
                if (request.Headers.Authorization?.Scheme == "Bearer")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Headers = { { "Docker-Content-Digest", "sha256:abc" } },
                        Content = new StringContent(
                            "{}",
                            new MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json")),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Headers =
                    {
                        WwwAuthenticate =
                        {
                            new AuthenticationHeaderValue(
                                "Bearer",
                                "realm=\"https://registry.example/token\",service=\"registry.example\",scope=\"repository:library/nginx:pull\""),
                        },
                    },
                };
            }

            if (request.RequestUri?.AbsolutePath == "/token")
            {
                Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
                Assert.Equal("registry.example", QueryParameter(request.RequestUri, "service"));
                Assert.Equal("repository:library/nginx:pull", QueryParameter(request.RequestUri, "scope"));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"mirror-token\"}"),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = CreateService(handler, username: "mirror-user", password: "mirror-pass");
        var surface = CreateSurface();

        var result = await service.TryFetchManifestAsync(
            surface,
            "library/nginx",
            "latest",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("sha256:abc", result.Digest);
        Assert.Equal(3, requests.Count);
        Assert.Equal("Basic", requests[0].Headers.Authorization?.Scheme);
        Assert.Equal("Basic", requests[1].Headers.Authorization?.Scheme);
        Assert.Equal("Bearer", requests[2].Headers.Authorization?.Scheme);
        Assert.Equal("mirror-token", requests[2].Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task TryFetchManifestAsync_DoesNotSendBasicCredentialsToInsecureTokenRealm()
    {
        var tokenRequests = 0;
        var handler = new CaptureRequestHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/v2/library/nginx/manifests/latest")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Headers =
                    {
                        WwwAuthenticate =
                        {
                            new AuthenticationHeaderValue(
                                "Bearer",
                                "realm=\"http://registry.example/token\",service=\"registry.example\",scope=\"repository:library/nginx:pull\""),
                        },
                    },
                };
            }

            if (request.RequestUri?.AbsolutePath == "/token")
            {
                tokenRequests++;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var service = CreateService(handler, username: "mirror-user", password: "mirror-pass");
        var surface = CreateSurface();

        var result = await service.TryFetchManifestAsync(
            surface,
            "library/nginx",
            "latest",
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(0, tokenRequests);
    }

    [Fact]
    public async Task TryFetchManifestAsync_TriesLowerPriorityRegistryAfterMiss()
    {
        var requestedHosts = new List<string>();
        var handler = new CaptureRequestHandler(request =>
        {
            requestedHosts.Add(request.RequestUri?.Host ?? string.Empty);
            if (request.RequestUri?.Host == "primary.example")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.RequestUri?.Host == "secondary.example")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Headers = { { "Docker-Content-Digest", "sha256:fallback" } },
                    Content = new StringContent(
                        "{}",
                        new MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json")),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });

        var service = CreateService(handler, registries: CreateFallbackRegistries());
        var surface = CreateSurface();

        var result = await service.TryFetchManifestAsync(
            surface,
            "library/nginx",
            "latest",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("sha256:fallback", result.Digest);
        Assert.Equal(new[] { "primary.example", "secondary.example" }, requestedHosts);
    }

    [Fact]
    public async Task TryFetchBlobAsync_TriesLowerPriorityRegistryAfterMiss()
    {
        var requestedHosts = new List<string>();
        var handler = new CaptureRequestHandler(request =>
        {
            requestedHosts.Add(request.RequestUri?.Host ?? string.Empty);
            if (request.RequestUri?.Host == "primary.example")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.RequestUri?.Host == "secondary.example")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([1, 2, 3]),
                };
            }

            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });

        var service = CreateService(handler, registries: CreateFallbackRegistries());
        var surface = CreateSurface();

        await using var stream = await service.TryFetchBlobAsync(
            surface,
            "library/nginx",
            "sha256:abc",
            TestContext.Current.CancellationToken);

        Assert.NotNull(stream);
        Assert.Equal(3, stream!.Length);
        Assert.Equal(new[] { "primary.example", "secondary.example" }, requestedHosts);
    }

    [Fact]
    public async Task TryCheckBlobExistsAsync_TriesLowerPriorityRegistryAfterMiss()
    {
        var requestedHosts = new List<string>();
        var handler = new CaptureRequestHandler(request =>
        {
            requestedHosts.Add(request.RequestUri?.Host ?? string.Empty);
            if (request.RequestUri?.Host == "primary.example")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.RequestUri?.Host == "secondary.example")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent([])
                    {
                        Headers = { ContentLength = 42 },
                    },
                };
            }

            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });

        var service = CreateService(handler, registries: CreateFallbackRegistries());
        var surface = CreateSurface();

        var result = await service.TryCheckBlobExistsAsync(
            surface,
            "library/nginx",
            "sha256:abc",
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Exists);
        Assert.Equal(42, result.Size);
        Assert.Equal(new[] { "primary.example", "secondary.example" }, requestedHosts);
    }

    [Fact]
    public void MirrorOrigin_MapsCacheOnlyMirrorsToCachedOrigin()
    {
        var service = CreateService(
            new CaptureRequestHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)),
            strategy: MirrorCachingStrategy.CacheOnly);

        var origin = service.MirrorOrigin(CreateSurface());

        Assert.Equal(PackageOrigin.Cached, origin);
    }

    private static OciMirrorService CreateService(
        HttpMessageHandler handler,
        string? username = null,
        string? password = null,
        MirrorCachingStrategy strategy = MirrorCachingStrategy.IndexAndCache,
        IEnumerable<OciUpstreamRegistryOptions>? registries = null)
    {
        var options = new OciFeedOptions
        {
            Mirror = new OciMirrorOptions
            {
                CachingStrategy = strategy,
                Registries = registries?.ToList() ??
                [
                    new OciUpstreamRegistryOptions
                    {
                        Url = "https://registry.example",
                        Priority = 0,
                        Username = username,
                        Password = password,
                    },
                ],
            },
        };

        var accessor = new OciFeedOptionsAccessor(new TestOptionsMonitor<OciFeedOptions>(options));
        var policy = new ConfigurableMirrorPolicyService(
            Options.Create(new SearchOptions()),
            Options.Create(new NpmFeedOptions()),
            new TestOptionsMonitor<OciFeedOptions>(options));
        return new OciMirrorService(
            accessor,
            policy,
            new SingleClientHttpClientFactory(new HttpClient(handler)),
            NullLogger<OciMirrorService>.Instance);
    }

    private static SurfaceContext CreateSurface() =>
        new(
            "default",
            FeedProtocol.Oci,
            "default",
            null,
            "/v2",
            new Uri("https://feed.example/v2/"));

    private static IReadOnlyList<OciUpstreamRegistryOptions> CreateFallbackRegistries() =>
    [
        new OciUpstreamRegistryOptions { Url = "https://secondary.example", Priority = 20 },
        new OciUpstreamRegistryOptions { Url = "https://primary.example", Priority = 10 },
    ];

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private static string? QueryParameter(Uri? uri, string key)
    {
        if (uri is null)
        {
            return null;
        }

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var item in query)
        {
            var parts = item.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == key)
            {
                return Uri.UnescapeDataString(parts[1]);
            }
        }

        return null;
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

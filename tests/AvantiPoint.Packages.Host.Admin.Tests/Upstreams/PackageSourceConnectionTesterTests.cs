using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;

namespace AvantiPoint.Packages.Host.Admin.Tests.Upstreams;

public sealed class PackageSourceConnectionTesterTests
{
    [Fact]
    public async Task TestAsync_ValidatesNuGetServiceIndex()
    {
        Uri? requestedUri = null;
        var tester = CreateTester(request =>
        {
            requestedUri = request.RequestUri;
            return Json(HttpStatusCode.OK, """{"version":"3.0.0","resources":[]}""");
        });

        var result = await tester.TestAsync(
            new PackageSource
            {
                Name = "NuGet",
                FeedUrl = "https://packages.example.test/v3/index.json",
                Protocol = PackageSourceProtocol.NuGet,
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("https://packages.example.test/v3/index.json", requestedUri?.AbsoluteUri);
    }

    [Fact]
    public async Task TestAsync_UsesNpmPingAndBearerToken()
    {
        Uri? requestedUri = null;
        AuthenticationHeaderValue? authorization = null;
        var tester = CreateTester(request =>
        {
            requestedUri = request.RequestUri;
            authorization = request.Headers.Authorization;
            return Json(HttpStatusCode.OK, "{}");
        });

        var result = await tester.TestAsync(
            new PackageSource
            {
                Name = "npm",
                FeedUrl = "https://npm.example.test/registry",
                Protocol = PackageSourceProtocol.Npm,
                ApiKey = "secret-token",
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("https://npm.example.test/registry/-/ping", requestedUri?.AbsoluteUri);
        Assert.Equal("Bearer", authorization?.Scheme);
        Assert.Equal("secret-token", authorization?.Parameter);
    }

    [Fact]
    public async Task TestAsync_RecognizesOciBearerChallengeAsReachable()
    {
        var tester = CreateTester(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            response.Headers.WwwAuthenticate.Add(
                new AuthenticationHeaderValue("Bearer", "realm=\"https://auth.example.test/token\""));
            return response;
        });

        var result = await tester.TestAsync(
            new PackageSource
            {
                Name = "OCI",
                FeedUrl = "https://registry.example.test",
                Protocol = PackageSourceProtocol.Oci,
            },
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Contains("bearer authentication", result.Message);
    }

    [Fact]
    public async Task TestAsync_RejectsAWebPageMasqueradingAsNuGetIndex()
    {
        var tester = CreateTester(_ => Json(HttpStatusCode.OK, """{"message":"hello"}"""));

        var result = await tester.TestAsync(
            new PackageSource
            {
                Name = "Not NuGet",
                FeedUrl = "https://packages.example.test",
                Protocol = PackageSourceProtocol.NuGet,
            },
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("not a NuGet v3 service index", result.Message);
    }

    private static PackageSourceConnectionTester CreateTester(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(
            new StubHttpClientFactory(new StubHttpMessageHandler(responseFactory)),
            new NullSecretProtector());

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) => new(statusCode)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}

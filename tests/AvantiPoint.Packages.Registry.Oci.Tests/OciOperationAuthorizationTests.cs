using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciOperationAuthorizationTests : IClassFixture<OciTestWebApplicationFactory>
{
    private const string ReadToken = "read-only-token";
    private const string WriteToken = "write-only-token";
    private readonly OciTestWebApplicationFactory _factory;

    public OciOperationAuthorizationTests(OciTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WriteOnlyCredential_CanCompleteNativeLoginProbeAndStartPush()
    {
        using var factory = CreateOperationAwareFactory();
        await EnsureDatabaseAsync(factory);
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var probe = new HttpRequestMessage(HttpMethod.Get, "/v2/");
        probe.Headers.Authorization = Basic("publisher", WriteToken);
        using var probeResponse = await client.SendAsync(probe, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, probeResponse.StatusCode);

        using var upload = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v2/test/{Guid.NewGuid():N}/blobs/uploads/");
        upload.Headers.Authorization = Bearer(WriteToken);
        using var uploadResponse = await client.SendAsync(upload, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task WriteOnlyCredential_CannotGainReadAccessThroughAPullScopedTokenRequest()
    {
        using var factory = CreateOperationAwareFactory();
        await EnsureDatabaseAsync(factory);
        using var client = factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var login = new HttpRequestMessage(HttpMethod.Get, "/token?service=default");
        login.Headers.Authorization = Basic("publisher", WriteToken);
        using var loginResponse = await client.SendAsync(login, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        using var pull = new HttpRequestMessage(
            HttpMethod.Get,
            "/token?service=default&scope=repository:private/repository:pull");
        pull.Headers.Authorization = Basic("publisher", WriteToken);
        using var pullResponse = await client.SendAsync(pull, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, pullResponse.StatusCode);
        using var payload = System.Text.Json.JsonDocument.Parse(await pullResponse.Content.ReadAsStringAsync(cancellationToken));
        using var read = new HttpRequestMessage(HttpMethod.Get, "/v2/private/repository/manifests/latest");
        read.Headers.Authorization = Bearer(payload.RootElement.GetProperty("token").GetString()!);
        using var readResponse = await client.SendAsync(read, cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, readResponse.StatusCode);
    }

    [Fact]
    public async Task WriteOnlyCredential_CanPerformPushExistenceChecks()
    {
        using var factory = CreateOperationAwareFactory();
        await EnsureDatabaseAsync(factory);
        using var client = factory.CreateClient();
        var repository = $"existence/{Guid.NewGuid():N}";
        var digest = $"sha256:{new string('0', 64)}";
        var cancellationToken = TestContext.Current.CancellationToken;

        using var manifestHead = new HttpRequestMessage(
            HttpMethod.Head,
            $"/v2/{repository}/manifests/latest");
        manifestHead.Headers.Authorization = Bearer(WriteToken);
        using var manifestResponse = await client.SendAsync(manifestHead, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, manifestResponse.StatusCode);

        using var blobHead = new HttpRequestMessage(
            HttpMethod.Head,
            $"/v2/{repository}/blobs/{digest}");
        blobHead.Headers.Authorization = Bearer(WriteToken);
        using var blobResponse = await client.SendAsync(blobHead, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, blobResponse.StatusCode);
    }

    [Theory]
    [InlineData("/v2/_catalog")]
    [InlineData("/v2/private/repository/tags/list")]
    [InlineData("/v2/private/repository/manifests/latest")]
    [InlineData("/v2/private/repository/blobs/sha256:0000000000000000000000000000000000000000000000000000000000000000")]
    public async Task WriteOnlyCredential_CannotReadRegistryContentOrDiscovery(string path)
    {
        using var factory = CreateOperationAwareFactory();
        await EnsureDatabaseAsync(factory);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = Bearer(WriteToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyCredential_CanUsePullEndpoints()
    {
        using var factory = CreateOperationAwareFactory();
        await EnsureDatabaseAsync(factory);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v2/_catalog");
        request.Headers.Authorization = Bearer(ReadToken);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private WebApplicationFactory<IntegrationTestApi.Program> CreateOperationAwareFactory() =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Feed:Authentication:AllowAnonymousPull"] = "false",
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPackageAuthenticationService>();
                services.AddSingleton<IPackageAuthenticationService, OperationAwareAuthenticationService>();
            });
        });

    private static async Task EnsureDatabaseAsync(
        WebApplicationFactory<IntegrationTestApi.Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        await context.RunMigrationsAsync(TestContext.Current.CancellationToken);
    }

    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);

    private static AuthenticationHeaderValue Basic(string username, string token) =>
        new(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{token}")));

    private sealed class OperationAwareAuthenticationService
        : IPackageAuthenticationService, IOperationAwarePackageAuthenticationService
    {
        public Task<NuGetAuthenticationResult> AuthenticateAsync(
            string apiKey,
            CancellationToken cancellationToken) =>
            AuthenticateAsync(apiKey, FeedOperation.Push, cancellationToken);

        public Task<NuGetAuthenticationResult> AuthenticateAsync(
            string username,
            string token,
            CancellationToken cancellationToken) =>
            AuthenticateAsync(username, token, FeedOperation.Pull, cancellationToken);

        public Task<NuGetAuthenticationResult> AuthenticateAsync(
            string token,
            FeedOperation operation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Authenticate(token, operation));

        public Task<NuGetAuthenticationResult> AuthenticateAsync(
            string username,
            string token,
            FeedOperation operation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Authenticate(token, operation));

        private static NuGetAuthenticationResult Authenticate(string token, FeedOperation operation)
        {
            var permitted = (operation == FeedOperation.Pull && token == ReadToken)
                || (operation == FeedOperation.Push && token == WriteToken);
            if (!permitted)
            {
                return NuGetAuthenticationResult.Fail("Operation not permitted.", "Test Registry");
            }

            var identity = new ClaimsIdentity("test");
            identity.AddClaim(new Claim(ClaimTypes.Name, token));
            return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

/// <summary>
/// Verifies OCI token authentication on a secured test host (IntegrationTestApi factory).
/// Native Docker/Helm CLI tests use <see cref="Infrastructure.OciFeedServerFixture"/> with auth disabled.
/// </summary>
public sealed class OciTokenAuthTests : IClassFixture<OciTestWebApplicationFactory>
{
    private const string ApiKey = "integration-test-key";
    private readonly OciTestWebApplicationFactory _factory;

    public OciTokenAuthTests(OciTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Token_WithBasicApiKey_ReturnsAccessToken()
    {
        await _factory.EnsureDatabaseMigratedAsync();
        var client = _factory.CreateClient();

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"user:{ApiKey}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/token?service=test&scope=repository:hello:push,pull");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var token = json.RootElement.GetProperty("token").GetString();
        Assert.Equal(ApiKey, token);
    }

    [Fact]
    public async Task DockerStyleAuthFlow_StartUploadSucceeds()
    {
        await _factory.EnsureDatabaseMigratedAsync();
        var client = _factory.CreateClient();
        var repository = $"hello-world/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        using var unauthenticated = _factory.CreateClient();
        var challengeResponse = await unauthenticated.PostAsync(
            $"/v2/{repository}/blobs/uploads/",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, challengeResponse.StatusCode);
        Assert.True(
            challengeResponse.Headers.WwwAuthenticate.ToString().Contains("/token", StringComparison.OrdinalIgnoreCase),
            "Expected Bearer challenge pointing at the token endpoint.");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"user:{ApiKey}"));
        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/token?service=test&scope=repository:{repository}:pull,push");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken))
            .RootElement.GetProperty("token")
            .GetString();
        Assert.False(string.IsNullOrEmpty(token));

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/v2/{repository}/blobs/uploads/");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await client.SendAsync(uploadRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Token_WithoutCredentials_ReturnsUnauthorized()
    {
        await _factory.EnsureDatabaseMigratedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/token?service=test", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

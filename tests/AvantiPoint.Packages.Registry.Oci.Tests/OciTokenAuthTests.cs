using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Registry.Oci.Tests.Infrastructure;
using AvantiPoint.Packages.Registry.Tests.Shared;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

/// <summary>
/// Verifies the OCI distribution token endpoint used by Docker/Helm clients before native CLI tests run in CI.
/// </summary>
[Collection(nameof(OciFeedServerCollection))]
public sealed class OciTokenAuthTests : IClassFixture<OciFeedServerFixture>
{
    private readonly OciFeedServerFixture _fixture;

    public OciTokenAuthTests(OciFeedServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Token_WithBasicApiKey_ReturnsAccessToken()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"user:{FeedTestServerHost.DefaultApiKey}"));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/token?service=test&scope=repository:hello:push,pull");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var response = await _fixture.Server.Client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var token = json.RootElement.GetProperty("token").GetString();
        Assert.Equal(FeedTestServerHost.DefaultApiKey, token);
    }

    [Fact]
    public async Task DockerStyleAuthFlow_StartUploadSucceeds()
    {
        var repository = $"hello-world/{Guid.NewGuid():N}";
        var cancellationToken = TestContext.Current.CancellationToken;

        using var unauthenticated = new HttpClient { BaseAddress = _fixture.Server.BaseAddress };
        var challengeResponse = await unauthenticated.PostAsync(
            $"/v2/{repository}/blobs/uploads/",
            null,
            cancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, challengeResponse.StatusCode);
        Assert.True(
            challengeResponse.Headers.WwwAuthenticate.ToString().Contains("/token", StringComparison.OrdinalIgnoreCase),
            "Expected Bearer challenge pointing at the token endpoint.");

        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"user:{FeedTestServerHost.DefaultApiKey}"));
        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/token?service=test&scope=repository:{repository}:pull,push");
        tokenRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        var tokenResponse = await _fixture.Server.Client.SendAsync(tokenRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var token = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken))
            .RootElement.GetProperty("token")
            .GetString();
        Assert.False(string.IsNullOrEmpty(token));

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/v2/{repository}/blobs/uploads/");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var uploadResponse = await _fixture.Server.Client.SendAsync(uploadRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.Accepted, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Token_WithoutCredentials_ReturnsUnauthorized()
    {
        using var client = new HttpClient { BaseAddress = _fixture.Server.BaseAddress };

        var response = await client.GetAsync(
            "/token?service=test",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

using System.Net.Http.Headers;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Registry.Oci;
using ComponentRegression.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace ComponentRegression;

public sealed class OciReferrersAuthenticationTests
{
    private const string Digest = "sha256:2e4feabc6786fb8cda612ce69ec6ae31ec965ad1a4806d133fbca6b31dc1e518";

    [Theory]
    [InlineData("appportal-devspace")]
    [InlineData("docker/appportal-devspace")]
    [InlineData("team/product/appportal-devspace")]
    [InlineData("team/referrers/appportal-devspace")]
    [InlineData("team/manifests/appportal-devspace")]
    [InlineData("team/blobs/uploads/appportal-devspace")]
    [InlineData("team/tags/list/appportal-devspace")]
    public async Task ChallengeNegotiatesExactRepositoryPullGrant(string repository)
    {
        using var host = new ScopedTokenTestHost();
        var adapter = Adapter(host);
        var http = host.Context();
        http.Request.Path = $"/v2/{repository}/referrers/{Digest}";
        http.Request.Method = "GET";
        http.Request.QueryString = new("?artifactType=application%2Fvnd.example.signature");
        var request = new FeedAuthenticationRequest(ScopedTokenTestHost.DefaultSurface, http, FeedOperation.Pull);
        var result = await adapter.AuthenticateAsync(request, TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        var challenge = result.ResponseHeaders!["WWW-Authenticate"];
        Assert.Equal($"Bearer realm=\"https://registry.invalid/token\",service=\"default\",scope=\"repository:{repository}:pull\"", challenge);
        Assert.True(OciRouteParser.TryParse(http.Request.Path, out var route));
        Assert.Equal(OciRouteKind.Referrers, route.Kind);
        Assert.Equal(repository, route.RepositoryName);
        Assert.Equal(Digest, route.Digest);

        // Use the challenge, as a client does, rather than hard-coding a successful scope.
        var scope = challenge.Split("scope=\"")[1].TrimEnd('"');
        var token = await host.Issue("?service=default&scope=" + Uri.EscapeDataString(scope));
        Assert.Equal(200, token.Status);
        Assert.Equal($"repository:{repository}:pull", token.Scope);
        http.Request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token).ToString();
        Assert.True((await OciAccessTokens.AuthenticateAsync(adapter, request, TestContext.Current.CancellationToken)).Succeeded);
        http.Request.Path = $"/v2/{repository}-other/referrers/{Digest}";
        Assert.False((await OciAccessTokens.AuthenticateAsync(adapter, request, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Bearer invalid")]
    [InlineData("Bearer publisher")]
    [InlineData("Basic !!!")]
    public async Task MissingInvalidAndPublishOnlyCredentialsRemainDenied(string header)
    {
        using var host = new ScopedTokenTestHost();
        var http = host.Context();
        http.Request.Path = $"/v2/docker/appportal-devspace/referrers/{Digest}";
        http.Request.Headers.Authorization = header;
        var result = await Adapter(host).AuthenticateAsync(new(ScopedTokenTestHost.DefaultSurface, http, FeedOperation.Pull), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Contains("scope=\"repository:docker/appportal-devspace:pull\"", result.ResponseHeaders!["WWW-Authenticate"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorizedDirectCredentialsStillWork(bool basic)
    {
        using var host = new ScopedTokenTestHost();
        var http = host.Context();
        http.Request.Path = $"/v2/docker/appportal-devspace/referrers/{Digest}";
        http.Request.Headers.Authorization = basic
            ? "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user@example.invalid:consumer")) : "Bearer consumer";
        Assert.True((await Adapter(host).AuthenticateAsync(new(ScopedTokenTestHost.DefaultSurface, http, FeedOperation.Pull), TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task LoginAndOtherRepositoryTokensDoNotAuthorizeReferrers()
    {
        using var host = new ScopedTokenTestHost();
        var login = await host.Issue("?service=default", "both");
        var other = await host.Issue("?scope=repository:other:pull", "both");
        var push = await host.Issue("?scope=repository:docker/appportal-devspace:push", "both");
        foreach (var issued in new[] { login, other, push })
        {
            var http = host.Context();
            http.Request.Path = $"/v2/docker/appportal-devspace/referrers/{Digest}";
            http.Request.Headers.Authorization = "Bearer " + issued.Token;
            var result = await OciAccessTokens.AuthenticateAsync(Adapter(host), new(ScopedTokenTestHost.DefaultSurface, http, FeedOperation.Pull), TestContext.Current.CancellationToken);
            Assert.False(result.Succeeded);
            Assert.Contains("scope=\"repository:docker/appportal-devspace:pull\"", result.ResponseHeaders!["WWW-Authenticate"]);
        }
    }

    [Theory]
    [InlineData("/v2/team/manifests/app/manifests/latest", FeedOperation.Pull, "team/manifests/app", "pull")]
    [InlineData("/v2/team/blobs/app/blobs/sha256:abcd", FeedOperation.Pull, "team/blobs/app", "pull")]
    [InlineData("/v2/team/app/tags/list", FeedOperation.Pull, "team/app", "pull")]
    [InlineData("/v2/team/app/blobs/uploads/", FeedOperation.Push, "team/app", "pull,push")]
    [InlineData("/v2/team/app/blobs/uploads/upload-id", FeedOperation.Push, "team/app", "pull,push")]
    public async Task ExistingRoutesRetainExactChallenges(string path, FeedOperation operation, string repository, string actions)
    {
        using var host = new ScopedTokenTestHost();
        var http = host.Context();
        http.Request.Path = path;
        var result = await Adapter(host).AuthenticateAsync(new(ScopedTokenTestHost.DefaultSurface, http, operation), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Contains($"scope=\"repository:{repository}:{actions}\"", result.ResponseHeaders!["WWW-Authenticate"]);
    }

    [Theory]
    [InlineData("/v2/")]
    [InlineData("/v2/_catalog")]
    [InlineData("/not-v2/team/referrers/sha256:abcd")]
    public async Task NonRepositoryRequestsDoNotInventRepositoryScope(string path)
    {
        using var host = new ScopedTokenTestHost();
        var http = host.Context();
        http.Request.Path = path;
        var result = await Adapter(host).AuthenticateAsync(new(ScopedTokenTestHost.DefaultSurface, http, FeedOperation.Pull), TestContext.Current.CancellationToken);
        Assert.DoesNotContain("scope=", result.ResponseHeaders!["WWW-Authenticate"]);
    }

    private static OciFeedAuthenticationAdapter Adapter(ScopedTokenTestHost host) =>
        new(host.Authentication, Options.Create(new FeedOptions()));
}

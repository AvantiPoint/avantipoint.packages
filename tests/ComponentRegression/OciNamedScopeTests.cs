using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Registry.Oci;
using ComponentRegression.TestInfrastructure;
using Microsoft.Extensions.Options;

namespace ComponentRegression;

public sealed class OciNamedScopeTests
{
    private static SurfaceContext Named => ScopedTokenTestHost.DefaultSurface with { SurfaceId = "named-docker", OciSegment = "docker", RoutePrefix = "/docker", PublicBaseUrl = new("https://registry.invalid/docker") };
    private const string Manifest = "/v2/appportal-devspace/manifests/latest";

    [Theory]
    [InlineData("appportal-devspace")]
    [InlineData("team/product/app")]
    [InlineData("docker/app")]
    [InlineData("team/referrers/app")]
    public async Task NamedChallengeUsesPublicRepositoryNamespace(string repository)
    {
        using var host = Host();
        var http = host.Context(Named);
        http.Request.Path = $"/v2/{repository}/referrers/sha256:" + new string('a',64);
        var adapter = new OciFeedAuthenticationAdapter(host.Authentication, Options.Create(new FeedOptions()));
        var result = await adapter.AuthenticateAsync(new(Named,http,FeedOperation.Pull),TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Contains($"scope=\"repository:docker/{repository}:pull\"",result.ResponseHeaders!["WWW-Authenticate"]);
    }

    [Fact]
    public async Task NativePromotionScopesGrantPushToExactlyTheNormalizedRepository()
    {
        using var host = Host();
        var token = await host.Issue("?scope=repository:docker/appportal-devspace:pull&scope=repository:docker/appportal-devspace:pull,push","both",Named);
        Assert.Equal(200,token.Status);
        Assert.Equal("repository:docker/appportal-devspace:pull,push",token.Scope);
        Assert.True(OciAccessTokens.TryRead(host.Context(Named),token.Token,out var ticket,out _));
        Assert.Equal("appportal-devspace",Assert.Single(ticket.Grants).Repository);
        Assert.True(await host.Authorize(token.Token,Manifest,FeedOperation.Push,Named));
        Assert.True(await host.Authorize(token.Token,Manifest,FeedOperation.Pull,Named));
        Assert.False(await host.Authorize(token.Token,"/v2/other/manifests/latest",FeedOperation.Push,Named));
        Assert.False(await host.Authorize(token.Token,"/v2/docker/appportal-devspace/manifests/latest",FeedOperation.Push,Named));
        Assert.False(await host.Authorize(token.Token,Manifest,FeedOperation.Push,Named with { FeedId="other" }));
        Assert.False(await host.Authorize(token.Token,Manifest,FeedOperation.Push,Named with { SurfaceId="named-helm",OciSegment="helm" }));
    }

    [Theory]
    [InlineData("consumer","pull",false,true)]
    [InlineData("publisher","push",true,false)]
    public async Task NamespaceTranslationRetainsThePermissionIntersection(string credential,string granted,bool push,bool pull)
    {
        using var host = Host();
        var token=await host.Issue("?scope=repository:docker/appportal-devspace:pull,push",credential,Named);
        Assert.Equal(200,token.Status);
        Assert.Equal("repository:docker/appportal-devspace:"+granted,token.Scope);
        Assert.Equal(push,await host.Authorize(token.Token,Manifest,FeedOperation.Push,Named));
        Assert.Equal(pull,await host.Authorize(token.Token,Manifest,FeedOperation.Pull,Named));
    }

    [Theory]
    [InlineData("appportal-devspace")]
    [InlineData("helm/appportal-devspace")]
    [InlineData("dockerish/appportal-devspace")]
    public async Task WrongOrMissingWireNamespaceDoesNotBecomeAResourceGrant(string repository)
    {
        using var host=Host();
        Assert.Equal(400,(await host.Issue("?scope=repository:"+repository+":pull","both",Named)).Status);
    }

    [Fact]
    public async Task RepositoryBeginningWithTheSurfaceNameRemainsADistinctResource()
    {
        using var host=Host();
        var token=await host.Issue("?scope=repository:docker/docker/appportal-devspace:pull","both",Named);
        Assert.Equal("repository:docker/docker/appportal-devspace:pull",token.Scope);
        Assert.True(await host.Authorize(token.Token,"/v2/docker/appportal-devspace/manifests/latest",FeedOperation.Pull,Named));
        Assert.False(await host.Authorize(token.Token,Manifest,FeedOperation.Pull,Named));
        var reminted=await host.Issue("?scope=repository:docker/appportal-devspace:pull,push",token.Token,Named,bearer:true);
        Assert.Equal(200,reminted.Status);
        Assert.Empty(reminted.Scope);
        Assert.False(await host.Authorize(reminted.Token,Manifest,FeedOperation.Pull,Named));
    }

    [Fact]
    public async Task ExistingInternallyBoundTokensAreNotReinterpretedOrExpanded()
    {
        using var host=Host();
        var token=OciAccessTokens.Protect(host.Context(Named),new(Named.FeedId,Named.SurfaceId,"both",[new("docker/appportal-devspace",["pull"])]),DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.True(await host.Authorize(token,"/v2/docker/appportal-devspace/manifests/latest",FeedOperation.Pull,Named));
        Assert.False(await host.Authorize(token,Manifest,FeedOperation.Pull,Named));
        var child=await host.Issue("?scope=repository:docker/appportal-devspace:pull",token,Named,bearer:true);
        Assert.Empty(child.Scope);
        var own=await host.Issue("?scope=repository:docker/docker/appportal-devspace:pull,push",token,Named,bearer:true);
        Assert.Equal("repository:docker/docker/appportal-devspace:pull",own.Scope);
        Assert.False(await host.Authorize(own.Token,"/v2/docker/appportal-devspace/manifests/latest",FeedOperation.Push,Named));
    }

    [Fact]
    public async Task InvalidCredentialsRemainUnauthorizedForCanonicalScopes()
    {
        using var host=Host();
        Assert.Equal(401,(await host.Issue("?scope=repository:docker/appportal-devspace:pull","invalid",Named)).Status);
    }

    private static ScopedTokenTestHost Host()
    {
        var host=new ScopedTokenTestHost();
        host.Authentication.AllFeeds=true; // Underlying identity permits these surfaces; scoped tokens must still isolate them.
        return host;
    }
}

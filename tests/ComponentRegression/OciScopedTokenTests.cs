using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Packages.Registry.Oci;
using ComponentRegression.TestInfrastructure;

namespace ComponentRegression;

public sealed class OciScopedTokenTests
{
    private const string Mixed = "?service=default&scope=repository:appportal-devspace:pull,push&scope=repository:docker/appportal-devspace:pull";
    private const string Manifest = "/v2/appportal-devspace/manifests/latest";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCredentials_RespectTheExistingAnonymousProviderDecision(bool anonymousPull)
    {
        using var host = new ScopedTokenTestHost();
        host.Authentication.AnonymousPull = anonymousPull;
        var issued = await host.Issue("?service=default&scope=repository:appportal-devspace:pull,push", credential: "");
        Assert.Equal(anonymousPull ? 200 : 401, issued.Status);
        if (anonymousPull)
        {
            Assert.Equal("repository:appportal-devspace:pull", issued.Scope);
            Assert.True(await host.Authorize(issued.Token, Manifest, FeedOperation.Pull));
            Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Push));
        }
    }

    [Fact]
    public async Task ConsumerReceivesOnlyPullForTheRealRepeatedMixedScopeRequest()
    {
        using var host = new ScopedTokenTestHost();
        var issued = await host.Issue(Mixed);
        Assert.Equal(200, issued.Status);
        Assert.Equal("repository:appportal-devspace:pull repository:docker/appportal-devspace:pull", issued.Scope);
        Assert.NotEqual("consumer", issued.Token);
        Assert.DoesNotContain("consumer", issued.Token);
        Assert.True(await host.Authorize(issued.Token, Manifest, FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Push));
        Assert.False(await host.Authorize(issued.Token, "/v2/appportal-devspace/blobs/uploads/", FeedOperation.Push));
    }

    [Fact]
    public async Task PublisherKeepsHeadExistenceChecksWithoutContentReadPermission()
    {
        using var host = new ScopedTokenTestHost();
        var issued = await host.Issue("?scope=repository:appportal-devspace:pull,push", "publisher");
        Assert.Equal(200, issued.Status);
        Assert.Equal("repository:appportal-devspace:push", issued.Scope);
        Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Pull));
        // The dispatcher retries a HEAD with Push only after Pull fails.
        Assert.True(await host.Authorize(issued.Token, Manifest, FeedOperation.Push));
        Assert.True(await host.Authorize(issued.Token, "/v2/appportal-devspace/blobs/sha256:" + new string('0',64), FeedOperation.Push));
    }

    [Theory]
    [InlineData("consumer", "push")]
    [InlineData("publisher", "pull")]
    public async Task ValidIdentityWithoutRequestedPermissionReceivesNoResourceGrant(string credential, string action)
    {
        using var host = new ScopedTokenTestHost();
        var issued = await host.Issue("?scope=repository:appportal-devspace:" + action, credential);
        Assert.Equal(200, issued.Status);
        Assert.Empty(issued.Scope);
        Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Push));
    }

    [Fact]
    public async Task ReadWriteCredentialCannotUseAnIssuedPullTokenForWritesOrOtherRepositories()
    {
        using var host = new ScopedTokenTestHost();
        var issued = await host.Issue("?scope=repository:appportal-devspace:pull", "both");
        Assert.True(await host.Authorize(issued.Token, Manifest, FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Push));
        Assert.False(await host.Authorize(issued.Token, "/v2/other/manifests/latest", FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token, "/v2/appportal-devspace-extra/tags/list", FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token, "/v2/_catalog", FeedOperation.Pull));
    }

    [Theory]
    [InlineData("another-feed", "oci-docker")]
    [InlineData("default", "oci-helm")]
    public async Task ScopedTokenCannotCrossFeedOrSurfaceEvenWhenTheKeyCan(string feed, string surface)
    {
        using var host = new ScopedTokenTestHost();
        host.Authentication.AllFeeds = true;
        var issued = await host.Issue(Mixed, "both");
        var target = new SurfaceContext(feed,FeedProtocol.Oci,surface,"helm","/helm",new Uri("https://registry.invalid/helm"));
        Assert.False(await host.Authorize(issued.Token, Manifest, FeedOperation.Pull,target));
        var remint = await host.Issue("?scope=repository:appportal-devspace:pull", issued.Token,target,bearer:true);
        Assert.Equal(401, remint.Status);
    }

    [Fact]
    public async Task AScopedTokenCannotBeRemintedWithAdditionalActionsOrRepositories()
    {
        using var host = new ScopedTokenTestHost();
        var parent = await host.Issue("?scope=repository:appportal-devspace:pull", "both");
        var child = await host.Issue("?scope=repository:appportal-devspace:pull,push%20repository:other:pull",parent.Token,bearer:true);
        Assert.Equal(200, child.Status);
        Assert.Equal("repository:appportal-devspace:pull", child.Scope);
        Assert.False(await host.Authorize(child.Token,Manifest,FeedOperation.Push));
        Assert.False(await host.Authorize(child.Token,"/v2/other/manifests/latest",FeedOperation.Pull));
        Assert.True(OciAccessTokens.TryRead(host.Context(),parent.Token,out _,out var parentExpiry));
        Assert.True(OciAccessTokens.TryRead(host.Context(),child.Token,out _,out var childExpiry));
        Assert.True(childExpiry <= parentExpiry);
    }

    [Theory]
    [InlineData("invalid", "user@example.invalid")]
    [InlineData("consumer", "wrong@example.invalid")]
    public async Task InvalidCredentialsRemainUnauthorized(string credential,string username)
    {
        using var host=new ScopedTokenTestHost();
        Assert.Equal(401,(await host.Issue(Mixed,credential,username:username)).Status);
    }

    [Fact]
    public async Task RevocationStillAppliesAfterIssuance()
    {
        using var host=new ScopedTokenTestHost();
        var issued=await host.Issue(Mixed);
        host.Authentication.Revoked=true;
        Assert.False(await host.Authorize(issued.Token,Manifest,FeedOperation.Pull));
        Assert.Equal(401,(await host.Issue(Mixed,issued.Token,bearer:true)).Status);
    }

    [Fact]
    public async Task TamperedAndExpiredTokensFailClosed()
    {
        using var host=new ScopedTokenTestHost();
        var issued=await host.Issue(Mixed);
        var index=issued.Token.Length/2;
        var tampered=issued.Token[..index]+(issued.Token[index]=='A'?'B':'A')+issued.Token[(index+1)..];
        Assert.False(await host.Authorize(tampered,Manifest,FeedOperation.Pull));
        var expired=OciAccessTokens.Protect(host.Context(),new OciAccessTicket("default","oci-docker","consumer",[new("appportal-devspace",["pull"])]),DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.False(await host.Authorize(expired,Manifest,FeedOperation.Pull));
    }

    [Fact]
    public async Task LoginTokenGrantsNoRepositoryOrCatalogAccess()
    {
        using var host=new ScopedTokenTestHost();
        var issued=await host.Issue("?service=default","both");
        Assert.Equal(200,issued.Status);
        Assert.True(await host.Authorize(issued.Token,"/v2/",FeedOperation.Login));
        Assert.False(await host.Authorize(issued.Token,Manifest,FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token,"/v2/_catalog",FeedOperation.Pull));
    }

    [Fact]
    public async Task CatalogGrantDoesNotGrantRepositoryContent()
    {
        using var host=new ScopedTokenTestHost();
        var issued=await host.Issue("?scope=registry:catalog:*");
        Assert.Equal(200,issued.Status);
        Assert.True(await host.Authorize(issued.Token,"/v2/_catalog",FeedOperation.Pull));
        Assert.False(await host.Authorize(issued.Token,Manifest,FeedOperation.Pull));
    }

    [Theory]
    [InlineData("?scope=repository:appportal-devspace:delete")]
    [InlineData("?scope=repository:../other:pull")]
    [InlineData("?scope=repository:repo:pull:push")]
    [InlineData("?scope=registry:other:*")]
    public async Task InvalidScopesDoNotBecomePullGrants(string query)
    {
        using var host=new ScopedTokenTestHost();
        Assert.Equal(400,(await host.Issue(query)).Status);
    }

    [Fact]
    public async Task WrongServiceAndWrongFeedRemainDenied()
    {
        using var host=new ScopedTokenTestHost();
        Assert.Equal(401,(await host.Issue("?service=other&scope=repository:repo:pull")).Status);
        Assert.Equal(401,(await host.Issue("?scope=repository:repo:pull",surface:ScopedTokenTestHost.DefaultSurface with {FeedId="other"})).Status);
    }
}

using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Authentication;

public sealed class DatabasePackageAuthenticationServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MigratedLegacyKey_UsesNormalHashScopeAndRevocationChecks(bool publisher)
    {
        // Synthetic legacy format, never a production credential.
        const string legacyKey = "0123456789abcdef0123456789abcdef";
        var fixture = await CreateFixtureAsync(publisher ? FeedTokenScope.ReadWrite : FeedTokenScope.Read);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;
        var token = await context.HostApiTokens.Include(t => t.User).SingleAsync(TestContext.Current.CancellationToken);
        token.TokenPrefix = legacyKey[..8];
        token.TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(legacyKey + "test-pepper"))).ToLowerInvariant();
        token.AllFeeds = false;
        token.User.CanPublish = publisher;
        token.Feeds.Add(new HostApiTokenFeed { FeedId = "test", SurfaceId = "nuget" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Surface.Current = Surface("nuget");
        Assert.True((await fixture.Service.AuthenticateAsync(legacyKey, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(publisher, (await fixture.Service.AuthenticateAsync(legacyKey, FeedOperation.Push, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await fixture.Service.AuthenticateAsync("wrong-user@example.test", legacyKey, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        foreach (var surface in new[] { "npm", "docker", "helm" })
        {
            fixture.Surface.Current = Surface(surface);
            Assert.False((await fixture.Service.AuthenticateAsync(legacyKey, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        }
        fixture.Surface.Current = Surface("nuget");
        token.Revoked = true;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False((await fixture.Service.AuthenticateAsync(legacyKey, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        token.Revoked = false;
        token.Expires = DateTimeOffset.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False((await fixture.Service.AuthenticateAsync(legacyKey, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        token.Expires = DateTimeOffset.UtcNow.AddHours(1);
        token.User.IsRevoked = true;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.False((await fixture.Service.AuthenticateAsync(legacyKey, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Theory]
    [InlineData("nuget", "nuget", true)]
    [InlineData("nuget", "npm", false)]
    [InlineData("npm", "docker", false)]
    [InlineData("docker", "helm", false)]
    [InlineData("helm", "docker", false)]
    [InlineData("helm", "helm", true)]
    public async Task SelectedFeed_IsEnforcedForPullAndPush(string selected, string requested, bool allowed)
    {
        var fixture = await CreateFixtureAsync(FeedTokenScope.ReadWrite);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;
        var token = await context.HostApiTokens.SingleAsync(TestContext.Current.CancellationToken);
        token.AllFeeds = false;
        token.Feeds.Add(new HostApiTokenFeed { FeedId = "test", SurfaceId = selected });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Surface.Current = Surface(requested);

        Assert.Equal(allowed, (await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Equal(allowed, (await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Push, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task RestrictedToken_WithNoSelectionsOrUnknownSurface_DeniesAccess()
    {
        var fixture = await CreateFixtureAsync(FeedTokenScope.ReadWrite);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;
        var token = await context.HostApiTokens.SingleAsync(TestContext.Current.CancellationToken);
        token.AllFeeds = false;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Surface.Current = Surface("nuget");
        Assert.False((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        token.Feeds.Add(new HostApiTokenFeed { FeedId = "test", SurfaceId = "nuget" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Surface.Current = null;
        Assert.False((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task HelmOnlyWriteToken_CanLoginThroughDockerProbe_ButCannotAccessDocker()
    {
        var fixture = await CreateFixtureAsync(FeedTokenScope.Write);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;
        var token = await context.HostApiTokens.SingleAsync(TestContext.Current.CancellationToken);
        token.AllFeeds = false;
        token.Feeds.Add(new HostApiTokenFeed { FeedId = "test", SurfaceId = "helm" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Surface.Current = Surface("docker");

        Assert.True((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Login, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Pull, TestContext.Current.CancellationToken)).Succeeded);
        Assert.False((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Push, TestContext.Current.CancellationToken)).Succeeded);
        fixture.Surface.Current = Surface("helm");
        Assert.True((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Push, TestContext.Current.CancellationToken)).Succeeded);
    }

    [Fact]
    public async Task TokenSelections_DoNotCrossFeedIdentity()
    {
        var fixture = await CreateFixtureAsync(FeedTokenScope.ReadWrite);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;
        var token = await context.HostApiTokens.SingleAsync(TestContext.Current.CancellationToken);
        token.AllFeeds = false;
        token.Feeds.Add(new HostApiTokenFeed { FeedId = "other-feed", SurfaceId = "helm" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Surface.Current = Surface("helm");
        Assert.False((await fixture.Service.AuthenticateAsync(fixture.Plaintext, FeedOperation.Login, TestContext.Current.CancellationToken)).Succeeded);
    }

    private static SurfaceContext Surface(string id) => new("test", id switch
    {
        "nuget" => FeedProtocol.NuGet,
        "npm" => FeedProtocol.Npm,
        _ => FeedProtocol.Oci,
    }, id, id is "helm" or "docker" ? id : null, "", new Uri("https://packages.test/"));

    [Fact]
    public async Task ReadOnlyToken_CanPullNpmNuGetAndOciButCannotPush()
    {
        var fixture = await CreateFixtureAsync(FeedTokenScope.Read);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;

        var pull = await fixture.Service.AuthenticateAsync(
            fixture.Plaintext,
            FeedOperation.Pull,
            TestContext.Current.CancellationToken);
        var push = await fixture.Service.AuthenticateAsync(
            fixture.Plaintext,
            FeedOperation.Push,
            TestContext.Current.CancellationToken);

        Assert.True(pull.Succeeded);
        Assert.True(pull.User!.IsInRole(FeedRoles.Consumer));
        Assert.False(pull.User.IsInRole(FeedRoles.Publisher));
        Assert.False(push.Succeeded);
    }

    [Fact]
    public async Task WriteOnlyToken_CanPushButCannotPull()
    {
        var fixture = await CreateFixtureAsync(FeedTokenScope.Write);
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;

        var pull = await fixture.Service.AuthenticateAsync(
            fixture.Plaintext,
            FeedOperation.Pull,
            TestContext.Current.CancellationToken);
        var push = await fixture.Service.AuthenticateAsync(
            fixture.Plaintext,
            FeedOperation.Push,
            TestContext.Current.CancellationToken);

        Assert.False(pull.Succeeded);
        Assert.True(push.Succeeded);
        Assert.True(push.User!.IsInRole(FeedRoles.Publisher));
        Assert.False(push.User.IsInRole(FeedRoles.Consumer));
    }

    [Fact]
    public async Task ConfiguredBootstrapApiKey_CanPullAndPushBeforeAnIdentityProviderExists()
    {
        var fixture = await CreateFixtureAsync(
            FeedTokenScope.Read,
            bootstrapApiKey: "bootstrap-secret");
        await using var connection = fixture.Connection;
        await using var context = fixture.Context;

        var pull = await fixture.Service.AuthenticateAsync(
            "any-client-name",
            "bootstrap-secret",
            FeedOperation.Pull,
            TestContext.Current.CancellationToken);
        var push = await fixture.Service.AuthenticateAsync(
            "bootstrap-secret",
            FeedOperation.Push,
            TestContext.Current.CancellationToken);

        Assert.True(pull.Succeeded);
        Assert.True(push.Succeeded);
        Assert.True(pull.User!.IsInRole(FeedRoles.Consumer));
        Assert.True(push.User!.IsInRole(FeedRoles.Publisher));
    }

    private static async Task<AuthenticationFixture> CreateFixtureAsync(
        FeedTokenScope scope,
        string? bootstrapApiKey = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var context = new HostSqliteContext(
            new DbContextOptionsBuilder<HostSqliteContext>()
                .UseSqlite(connection)
                .Options);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var settings = Options.Create(new HostSettings
        {
            ServerName = "Test Packages",
            TokenHashPepper = "test-pepper",
        });
        var hasher = new HostTokenHasher(settings);
        var generated = hasher.GenerateToken();
        var user = new HostUser
        {
            Email = $"test-{Guid.NewGuid():N}@example.test",
            Name = "Registry User",
            ApprovalStatus = HostUserApprovalStatus.Approved,
            CanConsume = true,
            CanPublish = true,
        };
        context.HostUsers.Add(user);
        context.HostApiTokens.Add(new HostApiToken
        {
            TokenPrefix = generated.Prefix,
            TokenHash = generated.Hash,
            Description = "test token",
            UserEmail = user.Email,
            User = user,
            Scopes = scope,
            Created = DateTimeOffset.UtcNow,
            Expires = DateTimeOffset.UtcNow.AddHours(1),
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var surface = new SurfaceContextAccessor();
        var registry = new FeedRegistry(new FeedContext("test", "test", ""));
        registry.Register(new SurfaceRegistration("docker", FeedProtocol.Oci, null, "", ""));
        registry.Register(new SurfaceRegistration("helm", FeedProtocol.Oci, "helm", "/helm", ""));
        var service = new DatabasePackageAuthenticationService(
            context,
            hasher,
            settings,
            Options.Create(new FeedOptions
            {
                Authentication = new FeedAuthenticationOptions { ApiKey = bootstrapApiKey },
            }),
            new HttpContextAccessor(),
            NullLogger<DatabasePackageAuthenticationService>.Instance,
            surface, registry);
        return new AuthenticationFixture(connection, context, service, generated.Plaintext, surface);
    }

    private sealed record AuthenticationFixture(
        SqliteConnection Connection,
        HostSqliteContext Context,
        DatabasePackageAuthenticationService Service,
        string Plaintext,
        SurfaceContextAccessor Surface);
}

using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Tokens;

public sealed class HostApiTokenServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HostSqliteContext _db;
    private readonly HostApiTokenService _service;
    private readonly HostTokenHasher _hasher;
    private const string Email = "member@example.test";

    public HostApiTokenServiceTests()
    {
        _connection.Open();
        _db = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.HostUsers.Add(new HostUser { Email = Email, CanConsume = true, CanPublish = false });
        _db.SaveChanges();
        var registry = new FeedRegistry(new FeedContext("engineering", "Engineering", ""));
        registry.Register(new SurfaceRegistration("nuget", FeedProtocol.NuGet, null, "", ""));
        registry.Register(new SurfaceRegistration("npm", FeedProtocol.Npm, null, "/npm", ""));
        _hasher = new HostTokenHasher(Options.Create(new HostSettings { TokenHashPepper = "test-pepper" }));
        _service = new(_db, _hasher, registry);
    }

    [Fact]
    public async Task SelectedFeedsAndExpiration_ArePersistedWithoutPlaintext()
    {
        var before = DateTimeOffset.UtcNow;
        var secret = await Create(FeedTokenScope.Read, false, ["nuget", "NPM", "npm"], 7);
        _db.ChangeTracker.Clear();
        var token = await _db.HostApiTokens.Include(t => t.Feeds).SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(token.AllFeeds);
        Assert.Equal(2, token.Feeds.Count);
        Assert.All(token.Feeds, feed => Assert.Equal("engineering", feed.FeedId));
        Assert.Equal(["npm", "nuget"], token.Feeds.Select(f => f.SurfaceId).Order());
        Assert.InRange(token.Expires, before.AddDays(7), DateTimeOffset.UtcNow.AddDays(7));
        Assert.NotEqual(secret, token.TokenHash);
        Assert.True(_hasher.Verify(secret, token.TokenHash));
        Assert.DoesNotContain(secret, (await _db.HostAuditEvents.SingleAsync(TestContext.Current.CancellationToken)).Detail!);
    }

    [Fact]
    public async Task AllFeeds_PreservesExplicitAllFeedIntent()
    {
        await Create(FeedTokenScope.Read, true, [], 30);
        var token = await _db.HostApiTokens.Include(t => t.Feeds).SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(token.AllFeeds);
        Assert.Empty(token.Feeds);
    }

    [Fact]
    public async Task Consumer_CannotMintWriteToken()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Create(FeedTokenScope.ReadWrite, true, [], 30));
        Assert.Empty(_db.HostApiTokens);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(366)]
    public async Task InvalidExpiration_IsRejected(int days) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Create(FeedTokenScope.Read, true, [], days));

    [Fact]
    public async Task InvalidSelectionsAndEmptyPermissions_AreRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Create(FeedTokenScope.Read, false, [], 30));
        await Assert.ThrowsAsync<ArgumentException>(() => Create(FeedTokenScope.Read, false, ["not-configured"], 30));
        await Assert.ThrowsAsync<ArgumentException>(() => Create(FeedTokenScope.None, true, [], 30));
        Assert.Empty(_db.HostApiTokens);
    }

    [Fact]
    public async Task Revoke_RequiresOwnershipAndRecordsEvent()
    {
        await Create(FeedTokenScope.Read, true, [], 30);
        var token = await _db.HostApiTokens.SingleAsync(TestContext.Current.CancellationToken);
        Assert.False(await _service.RevokeAsync("someone-else@example.test", token.Id, TestContext.Current.CancellationToken));
        Assert.False(token.Revoked);
        Assert.True(await _service.RevokeAsync(Email, token.Id, TestContext.Current.CancellationToken));
        Assert.True(token.Revoked);
        Assert.Equal(2, await _db.HostAuditEvents.CountAsync(TestContext.Current.CancellationToken));
    }

    private Task<string> Create(FeedTokenScope permissions, bool allFeeds, string[] feeds, int days) =>
        _service.CreateAsync(Email, "CI", permissions, allFeeds, feeds, days, TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
}

using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class HostUserManagementServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly HostSqliteContext _db;
    private readonly HostAuthenticationOptions _options = new();
    private readonly HostUserManagementService _service;
    private const string Admin = "admin@example.test";
    private const string Member = "member@example.test";

    public HostUserManagementServiceTests()
    {
        _connection.Open();
        _db = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.HostUsers.AddRange(new HostUser { Email = Admin, IsAdmin = true, CanPublish = true },
            new HostUser { Email = Member });
        _db.SaveChanges();
        _service = new(_db, Options.Create(_options));
    }

    [Fact]
    public async Task RevokeRestoreApprove_RequiresNewLoginAndNewTokens()
    {
        var member = await _db.HostUsers.SingleAsync(u => u.Email == Member, TestContext.Current.CancellationToken);
        member.CanPublish = true;
        member.Tokens.Add(new HostApiToken { TokenHash = "previous-token", UserEmail = Member, Expires = DateTimeOffset.UtcNow.AddDays(10) });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.True((await Change(HostUserChange.Revoke)).Succeeded);
        Assert.True(member.IsRevoked);
        Assert.Equal(1, member.SessionVersion);
        Assert.True(Assert.Single(member.Tokens).Revoked);

        Assert.True((await Change(HostUserChange.Restore)).Succeeded);
        Assert.False(member.IsRevoked);
        Assert.Equal(2, member.SessionVersion);
        Assert.Equal(HostUserApprovalStatus.Pending, member.ApprovalStatus);
        Assert.False(member.CanConsume);
        Assert.False(member.CanPublish);
        Assert.False(member.IsAdmin);
        Assert.True((await Change(HostUserChange.Approve)).Succeeded);
        Assert.True(member.CanConsume);
        Assert.True(Assert.Single(member.Tokens).Revoked);
        Assert.Equal(3, await _db.HostAuditEvents.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HostUserChange.ToggleAdmin)]
    [InlineData(HostUserChange.TogglePublish)]
    [InlineData(HostUserChange.Approve)]
    [InlineData(HostUserChange.Revoke)]
    [InlineData(HostUserChange.Restore)]
    public async Task Consumer_CannotManageUsers(HostUserChange change)
    {
        Assert.False((await _service.ChangeAsync(Member, Admin, change, TestContext.Current.CancellationToken)).Succeeded);
        Assert.Empty(_db.HostAuditEvents);
    }

    [Theory]
    [InlineData(HostUserChange.ToggleAdmin)]
    [InlineData(HostUserChange.Revoke)]
    public async Task Administrator_CannotRemoveOwnAccess(HostUserChange change)
    {
        Assert.False((await _service.ChangeAsync(Admin, Admin, change, TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await _db.HostUsers.SingleAsync(u => u.Email == Admin, TestContext.Current.CancellationToken)).IsAdmin);
    }

    [Fact]
    public async Task EntraManagedPermissions_CannotBeGrantedLocally()
    {
        _options.Microsoft.AdminRoleGroupIds = [Guid.NewGuid().ToString()];
        _options.Microsoft.PublisherRoleGroupIds = [Guid.NewGuid().ToString()];
        _options.Microsoft.ConsumerRoleGroupIds = [Guid.NewGuid().ToString()];
        Assert.False((await Change(HostUserChange.ToggleAdmin)).Succeeded);
        Assert.False((await Change(HostUserChange.TogglePublish)).Succeeded);
        Assert.True((await Change(HostUserChange.Approve)).Succeeded);
        Assert.False((await _db.HostUsers.SingleAsync(u => u.Email == Member, TestContext.Current.CancellationToken)).CanConsume);
    }

    [Fact]
    public async Task LocalPublishingGrant_IsAuditedAndCanBeRemoved()
    {
        Assert.True((await Change(HostUserChange.TogglePublish)).Succeeded);
        Assert.True((await _db.HostUsers.SingleAsync(u => u.Email == Member, TestContext.Current.CancellationToken)).CanPublish);
        Assert.True((await Change(HostUserChange.TogglePublish)).Succeeded);
        Assert.False((await _db.HostUsers.SingleAsync(u => u.Email == Member, TestContext.Current.CancellationToken)).CanPublish);
        Assert.All(await _db.HostAuditEvents.ToListAsync(TestContext.Current.CancellationToken), e => Assert.Equal(Admin, e.Actor));
    }

    private Task<HostUserChangeResult> Change(HostUserChange change) => _service.ChangeAsync(Admin, Member, change, TestContext.Current.CancellationToken);
    public async ValueTask DisposeAsync() { await _db.DisposeAsync(); await _connection.DisposeAsync(); }
}

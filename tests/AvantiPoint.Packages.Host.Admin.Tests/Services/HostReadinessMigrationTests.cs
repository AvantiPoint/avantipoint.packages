using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class HostReadinessMigrationTests
{
    [Fact]
    public async Task Upgrade_PreservesExistingUsersAndAllFeedTokens()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var context = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(connection).Options);
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20260717133008_AddPublishTargetUsername", TestContext.Current.CancellationToken);
        await context.Database.ExecuteSqlRawAsync("""
            INSERT INTO HostUsers (Email, Name, IsAdmin, CanPublish, CanConsume, IsRevoked, ApprovalStatus, ExternalProvider, CreatedAt)
            VALUES ('upgrade@example.test', 'Upgrade test', 1, 1, 1, 0, 0, 0, '2026-09-12T00:00:00Z');
            INSERT INTO HostApiTokens (TokenPrefix, TokenHash, Description, UserEmail, Scopes, Created, Expires, Revoked, IsSystemToken)
            VALUES ('apt_test', 'unchanged-test-hash', 'Existing token', 'upgrade@example.test', 3, '2026-09-12T00:00:00Z', '2027-09-12T00:00:00Z', 0, 0);
            """, TestContext.Current.CancellationToken);
        await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        var user = await context.HostUsers.SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(user.IsAdmin);
        Assert.Equal(0, user.SessionVersion);
        var token = await context.HostApiTokens.Include(t => t.Feeds).SingleAsync(TestContext.Current.CancellationToken);
        Assert.True(token.AllFeeds);
        Assert.Empty(token.Feeds);
        Assert.Equal("unchanged-test-hash", token.TokenHash);
        Assert.Empty(await context.HostWebhookDeliveries.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.HostWebhookCheckpoints.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }
}

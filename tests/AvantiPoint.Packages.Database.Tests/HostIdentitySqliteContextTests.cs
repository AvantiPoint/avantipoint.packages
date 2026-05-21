using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Database.Tests;

public class HostIdentitySqliteContextTests : IDisposable
{
    private readonly List<SqliteConnection> _connections = [];

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Close();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task PackageAndHostIdentityContexts_MigrateOnSharedDatabase()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var packageOptions = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;
        var hostOptions = new DbContextOptionsBuilder<HostSqliteContext>()
            .UseSqlite(connection, sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"))
            .Options;

        await using (var packageContext = new SqliteContext(packageOptions))
        {
            await packageContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await using (var hostContext = new HostSqliteContext(hostOptions))
        {
            await hostContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await using var verifyPackage = new SqliteContext(packageOptions);
        await using var verifyHost = new HostSqliteContext(hostOptions);

        Assert.True(await verifyPackage.Database.CanConnectAsync(TestContext.Current.CancellationToken));
        Assert.True(await verifyHost.Database.CanConnectAsync(TestContext.Current.CancellationToken));

        var tables = await verifyPackage.Database.SqlQueryRaw<string>(
                "SELECT name FROM sqlite_master WHERE type='table'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Contains("HostUsers", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
        Assert.Contains("__HostIdentityMigrationsHistory", tables);
    }
}

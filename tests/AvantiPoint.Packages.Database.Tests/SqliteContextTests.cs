using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Database.Tests;

public class SqliteContextTests(ITestOutputHelper output) : IDisposable
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

    private async Task WithMigratedContextAsync(Func<SqliteContext, CancellationToken, Task> test)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        await test(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public Task CanMigrate() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanMigrateAsync(context, ct));

    [Fact]
    public Task CanInsertAndQueryTestData() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanInsertAndQueryTestDataAsync(context, ct));

    [Fact]
    public Task CanQueryWithIndexedColumns() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanQueryWithIndexedColumnsAsync(context, ct));

    [Fact]
    public Task CanTrackPackageDownloads() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanTrackPackageDownloadsAsync(context, ct));

    [Fact]
    public Task ViewsExistAndAreQueryable() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.ViewsExistAndAreQueryableAsync(context, output, ct));

    [Fact]
    public Task IndexesExist() =>
        WithMigratedContextAsync((context, ct) =>
            DatabaseContextTestScenarios.IndexesExistAsync(context, DatabaseProviderKind.Sqlite, output, ct));

    [Fact]
    public Task ViewsExist() =>
        WithMigratedContextAsync((context, ct) =>
            DatabaseContextTestScenarios.ViewsExistAsync(context, DatabaseProviderKind.Sqlite, output, ct));

    [Fact]
    public Task CanUseSigningAndVulnerabilityTables() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanUseSigningAndVulnerabilityTablesAsync(context, ct));
}

using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Database.Tests;

public class SqlServerContextTests(SqlServerTestcontainerFixture fixture, ITestOutputHelper output)
    : IClassFixture<SqlServerTestcontainerFixture>
{
    private async Task WithMigratedContextAsync(Func<SqlServerContext, CancellationToken, Task> test)
    {
        var handle = await fixture.CreateDatabaseAsync();

        try
        {
            var options = new DbContextOptionsBuilder<SqlServerContext>()
                .UseSqlServer(handle.ConnectionString)
                .Options;

            await using var context = new SqlServerContext(options);
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
            await test(context, TestContext.Current.CancellationToken);
        }
        finally
        {
            await fixture.DropDatabaseAsync(handle.DatabaseName);
        }
    }

    [DockerFact]
    public Task CanMigrate() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanMigrateAsync(context, ct));

    [DockerFact]
    public Task CanInsertAndQueryTestData() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanInsertAndQueryTestDataAsync(context, ct));

    [DockerFact]
    public Task CanQueryWithIndexedColumns() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanQueryWithIndexedColumnsAsync(context, ct));

    [DockerFact]
    public Task CanTrackPackageDownloads() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanTrackPackageDownloadsAsync(context, ct));

    [DockerFact]
    public Task ViewsExistAndAreQueryable() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.ViewsExistAndAreQueryableAsync(context, output, ct));

    [DockerFact]
    public Task IndexesExist() =>
        WithMigratedContextAsync((context, ct) =>
            DatabaseContextTestScenarios.IndexesExistAsync(context, DatabaseProviderKind.SqlServer, output, ct));

    [DockerFact]
    public Task ViewsExist() =>
        WithMigratedContextAsync((context, ct) =>
            DatabaseContextTestScenarios.ViewsExistAsync(context, DatabaseProviderKind.SqlServer, output, ct));

    [DockerFact]
    public Task CanUseSigningAndVulnerabilityTables() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanUseSigningAndVulnerabilityTablesAsync(context, ct));

    /// <summary>
    /// Ensures signing migrations apply on databases that already have vulnerability support
    /// (regression test for duplicate-table migrations).
    /// </summary>
    [DockerFact]
    public Task IncrementalMigrate_FromVulnerabilitySupport_AddsSigningSchema()
    {
        return WithContextAtMigrationAsync(
            "20251117003704_AddVulnerabilitySupport",
            async (context, ct) =>
            {
                await context.Database.MigrateAsync(ct);
                await DatabaseContextTestScenarios.CanUseSigningAndVulnerabilityTablesAsync(context, ct);
            });
    }

    private async Task WithContextAtMigrationAsync(
        string targetMigration,
        Func<SqlServerContext, CancellationToken, Task> test)
    {
        var handle = await fixture.CreateDatabaseAsync();

        try
        {
            var options = new DbContextOptionsBuilder<SqlServerContext>()
                .UseSqlServer(handle.ConnectionString)
                .Options;

            await using var context = new SqlServerContext(options);
            await context.Database.MigrateAsync(targetMigration, TestContext.Current.CancellationToken);
            await test(context, TestContext.Current.CancellationToken);
        }
        finally
        {
            await fixture.DropDatabaseAsync(handle.DatabaseName);
        }
    }
}

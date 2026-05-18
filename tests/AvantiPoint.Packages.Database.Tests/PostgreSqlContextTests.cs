using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.PostgreSql;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Database.Tests;

public class PostgreSqlContextTests(PostgreSqlTestcontainerFixture fixture, ITestOutputHelper output)
    : IClassFixture<PostgreSqlTestcontainerFixture>
{
    private async Task WithMigratedContextAsync(Func<PostgreSqlContext, CancellationToken, Task> test)
    {
        var handle = await fixture.CreateDatabaseAsync();

        try
        {
            var options = new DbContextOptionsBuilder<PostgreSqlContext>()
                .UseNpgsql(handle.ConnectionString)
                .Options;

            await using var context = new PostgreSqlContext(options);
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
            DatabaseContextTestScenarios.IndexesExistAsync(context, DatabaseProviderKind.PostgreSql, output, ct));

    [DockerFact]
    public Task ViewsExist() =>
        WithMigratedContextAsync((context, ct) =>
            DatabaseContextTestScenarios.ViewsExistAsync(context, DatabaseProviderKind.PostgreSql, output, ct));

    [DockerFact]
    public Task CanUseSigningAndVulnerabilityTables() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanUseSigningAndVulnerabilityTablesAsync(context, ct));
}

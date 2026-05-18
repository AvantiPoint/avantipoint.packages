using AvantiPoint.Packages.Database.MySql;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Database.Tests;

[Collection("MySqlDatabase")]
public class MySqlContextTests(MySqlTestcontainerFixture fixture, ITestOutputHelper output)
{
    private async Task WithMigratedContextAsync(Func<MySqlContext, CancellationToken, Task> test)
    {
        var handle = await fixture.CreateDatabaseAsync();

        try
        {
            var options = new DbContextOptionsBuilder<MySqlContext>()
                .UseMySQL(handle.ConnectionString)
                .Options;

            await using var context = new MySqlContext(options);
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
            DatabaseContextTestScenarios.IndexesExistAsync(context, DatabaseProviderKind.MySql, output, ct));

    [DockerFact]
    public Task ViewsExist() =>
        WithMigratedContextAsync((context, ct) =>
            DatabaseContextTestScenarios.ViewsExistAsync(context, DatabaseProviderKind.MySql, output, ct));

    [DockerFact]
    public Task CanUseSigningAndVulnerabilityTables() =>
        WithMigratedContextAsync((context, ct) => DatabaseContextTestScenarios.CanUseSigningAndVulnerabilityTablesAsync(context, ct));
}

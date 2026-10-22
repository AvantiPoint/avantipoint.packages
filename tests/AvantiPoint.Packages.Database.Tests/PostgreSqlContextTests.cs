using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.PostgreSql;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NuGet.Versioning;

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
    public async Task CaseInsensitiveIdentityUpgrade_ReportsCollisionsWithoutChangingAnyPackage()
    {
        var ct = TestContext.Current.CancellationToken;
        var handle = await fixture.CreateDatabaseAsync();
        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(handle.ConnectionString) { IncludeErrorDetail = true }.ConnectionString;
            var options = new DbContextOptionsBuilder<PostgreSqlContext>().UseNpgsql(connectionString).Options;
            await using var context = new PostgreSqlContext(options);
            await context.Database.MigrateAsync("20260912231835_AddOciMutationGate", ct);
            var packages = new[]
            {
                new Package { Id = "Foo", Version = NuGetVersion.Parse("1.0.0"), Description = "first private contents", Published = DateTime.UtcNow },
                new Package { Id = "foo", Version = NuGetVersion.Parse("1.0.0"), Description = "different private contents", Published = DateTime.UtcNow },
                new Package { Id = "Foo", FeedId = "another-feed", Version = NuGetVersion.Parse("1.0.0"), Description = "separate feed", Published = DateTime.UtcNow },
            };
            context.Packages.AddRange(packages);
            await context.SaveChangesAsync(ct);
            var before = await context.Packages.AsNoTracking().OrderBy(p => p.Key)
                .Select(p => new { p.Key, p.Id, p.FeedId, p.NormalizedVersionString, p.Description }).ToArrayAsync(ct);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync(ct));
            Assert.Equal("23505", exception.SqlState);
            Assert.Contains("1 case-insensitive collision group(s)", exception.MessageText);
            Assert.Contains("No package rows were changed", exception.MessageText);
            Assert.Contains("Do not automatically merge or delete", exception.Hint);
            using var report = System.Text.Json.JsonDocument.Parse(exception.Detail!);
            var group = Assert.Single(report.RootElement.EnumerateArray());
            Assert.Equal("default", group.GetProperty("feed_id").GetString());
            Assert.Equal(new[] { packages[0].Key, packages[1].Key }, group.GetProperty("package_keys").EnumerateArray().Select(key => key.GetInt32()));
            Assert.DoesNotContain("20260913192500_AddCaseInsensitiveNuGetIdentity", await context.Database.GetAppliedMigrationsAsync(ct));
            var after = await context.Packages.AsNoTracking().OrderBy(p => p.Key)
                .Select(p => new { p.Key, p.Id, p.FeedId, p.NormalizedVersionString, p.Description }).ToArrayAsync(ct);
            Assert.Equal(before, after);
            // Retrying remains safe and cannot silently pick a row to keep.
            await Assert.ThrowsAsync<PostgresException>(() => context.Database.MigrateAsync(ct));
            Assert.Equal(3, await context.Packages.CountAsync(ct));
        }
        finally { await fixture.DropDatabaseAsync(handle.DatabaseName); }
    }

    [DockerFact]
    public async Task CaseInsensitiveIdentityUpgrade_PreservesDistinctFeedsAndVersions()
    {
        var ct = TestContext.Current.CancellationToken;
        var handle = await fixture.CreateDatabaseAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PostgreSqlContext>().UseNpgsql(handle.ConnectionString).Options;
            await using var context = new PostgreSqlContext(options);
            await context.Database.MigrateAsync("20260912231835_AddOciMutationGate", ct);
            context.Packages.AddRange(
                new Package { Id = "Foo", Version = NuGetVersion.Parse("1.0.0"), Description = "version one", Published = DateTime.UtcNow },
                new Package { Id = "foo", Version = NuGetVersion.Parse("2.0.0"), Description = "version two", Published = DateTime.UtcNow },
                new Package { Id = "FOO", FeedId = "another-feed", Version = NuGetVersion.Parse("1.0.0"), Description = "other feed", Published = DateTime.UtcNow });
            await context.SaveChangesAsync(ct);
            var before = await context.Packages.AsNoTracking().OrderBy(p => p.Key)
                .Select(p => new { p.Key, p.Id, p.FeedId, p.NormalizedVersionString, p.Description }).ToArrayAsync(ct);
            await context.Database.MigrateAsync(ct);
            var after = await context.Packages.AsNoTracking().OrderBy(p => p.Key)
                .Select(p => new { p.Key, p.Id, p.FeedId, p.NormalizedVersionString, p.Description }).ToArrayAsync(ct);
            Assert.Equal(before, after);
            context.Packages.Add(new Package { Id = "fOO", Version = NuGetVersion.Parse("1.0.0"), Published = DateTime.UtcNow });
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(ct));
            Assert.Equal("23505", Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        }
        finally { await fixture.DropDatabaseAsync(handle.DatabaseName); }
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

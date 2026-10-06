using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;

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

    [DockerFact]
    public Task ConcurrentNativePublicationsRecoverFromDuplicateUniqueIndexKeys() =>
        WithMigratedContextAsync(async (context, ct) =>
        {
            var directory = Directory.CreateTempSubdirectory("native-sql-race-");
            try
            {
                var storage = new FileStorageService(Options.Create(new FileSystemStorageOptions { Path = directory.FullName }));
                var registry = new FeedRegistry(new FeedContext("race-test", "race-test", "race-test"));
                var surface = new SurfaceContext("race-test", FeedProtocol.Maven, "maven", null, "/maven", new Uri("https://registry.test/maven/"));
                foreach (var sameBytes in new[] { true, false })
                {
                    var barrier = new PublicationBarrier();
                    var options = new DbContextOptionsBuilder<SqlServerContext>().UseSqlServer(context.Database.GetConnectionString())
                        .AddInterceptors(barrier).Options;
                    await using var firstContext = new SqlServerContext(options);
                    await using var secondContext = new SqlServerContext(options);
                    var first = new NativeArtifactStore(firstContext, storage, registry);
                    var second = new NativeArtifactStore(secondContext, storage, registry);
                    var version = sameBytes ? "1.0.0" : "2.0.0";
                    var path = $"com/example/sdk/{version}/sdk-{version}.jar";
                    await using var firstUpload = await ArtifactUpload.ReadAsync(new MemoryStream("first"u8.ToArray()), 100, ct);
                    await using var secondUpload = await ArtifactUpload.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(sameBytes ? "first" : "second")), 100, ct);
                    var results = await Task.WhenAll(
                        first.PutAsync(surface, path, "com.example:sdk", version, "application/java-archive", firstUpload, null, ct),
                        second.PutAsync(surface, path, "com.example:sdk", version, "application/java-archive", secondUpload, null, ct));

                    Assert.Single(results, result => result == StoragePutResult.Success);
                    Assert.Single(results, result => result == (sameBytes ? StoragePutResult.AlreadyExists : StoragePutResult.Conflict));
                    // This proves the actual SQL Server error, rather than fabricating SqlException.
                    Assert.Contains(2601, barrier.ErrorCodes);
                    var artifact = await context.NativeArtifacts.AsNoTracking().SingleAsync(artifact => artifact.Version == version, ct);
                    Assert.Equal(results[0] == StoragePutResult.Success ? firstUpload.Sha256 : secondUpload.Sha256, artifact.ContentHash);
                    using var downloaded = new MemoryStream();
                    await first.CopyToAsync(artifact, downloaded, ct);
                    Assert.Equal(sameBytes || results[0] == StoragePutResult.Success ? "first" : "second", Encoding.UTF8.GetString(downloaded.ToArray()));
                }
            }
            finally { directory.Delete(recursive: true); }
        });

    private sealed class PublicationBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public ConcurrentBag<int> ErrorCodes { get; } = [];

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            // Both requests have already observed the logical artifact as absent.
            if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Exception.GetBaseException() is SqlException sql)
                foreach (SqlError error in sql.Errors) ErrorCodes.Add(error.Number);
            return Task.CompletedTask;
        }
    }

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

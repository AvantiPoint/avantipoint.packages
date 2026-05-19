using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Search.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Search.Tests;

public class SearchReconciliationTests
{
    [Fact]
    public async Task NullSearchIndexer_ReconciliationCompletesWithoutIndexing()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"search-reconcile-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<SqliteContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            await using (var setup = new SqliteContext(options))
            {
                await setup.Database.MigrateAsync();
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<SqliteContext>(o => o.UseSqlite($"Data Source={dbPath}"));
            services.AddScoped<IContext>(sp => sp.GetRequiredService<SqliteContext>());
            services.AddSingleton<NullSearchIndexer>();
            services.AddSingleton<ISearchIndexer>(sp => sp.GetRequiredService<NullSearchIndexer>());
            services.AddSingleton<IOptions<SearchOptions>>(new TestOptions<SearchOptions>(
                new SearchOptions { Type = "Database", ReconcileBatchSize = 50 }));
            services.AddSingleton<IUrlGenerator>(_ => new TestUrlGenerator());
            services.AddSingleton<SearchDocumentMapper>();
            services.AddSingleton<IPackageSearchDocumentFactory, PackageSearchDocumentFactory>();
            services.AddTransient<ISearchIndexingService, SearchIndexingService>();
            services.AddHostedService<SearchIndexReconciliationHostedService>();

            await using var provider = services.BuildServiceProvider();
            var reconcile = provider.GetServices<IHostedService>()
                .OfType<SearchIndexReconciliationHostedService>()
                .Single();
            await reconcile.StartAsync(CancellationToken.None);
            await Task.Delay(500);
            await reconcile.StopAsync(CancellationToken.None);

            await provider.DisposeAsync();

            await using (var context = new SqliteContext(options))
            {
                var indexed = await context.Packages.AnyAsync(p => p.IndexedWith != null);
                Assert.False(indexed);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}

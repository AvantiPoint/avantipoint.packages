using System.IO;
using System.Linq;
using AvantiPoint.Packages.Core;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGet.Versioning;
using SampleDataGenerator;

namespace AvantiPoint.Packages.UI.Tests;

// Factory for the sample OpenFeed application
public class OpenFeedFactory : WebApplicationFactory<OpenFeed.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Create a unique temp folder for storage for each test run
        var storagePath = Path.Combine(Path.GetTempPath(), $"OpenFeedTests-{Guid.NewGuid():N}");
        
        // Create a unique in-memory database name for each test run
        var dbName = $"test-{Guid.NewGuid():N}";
        
        // Configure for in-memory Sqlite database with a fresh database each time
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Disable the sample data seeder from OpenFeed.Program so we can control test data
                { "DisableSampleDataSeeder", "true" },
                // The search and autocomplete APIs are public pull operations in this test host.
                { "Feed:Authentication:AllowAnonymousPull", "true" },
                // Force Sqlite database type
                { "Database:Type", "Sqlite" },
                // Use unique in-memory database for each test - shared cache so migrations work
                { "ConnectionStrings:Sqlite", $"Data Source=file:{dbName}?mode=memory&cache=shared" },
                // Configure file storage with temp folder
                { "Storage:Type", "FileSystem" },
                { "Storage:Path", storagePath }
            });
        });

        builder.ConfigureLogging(logging =>
        {
            logging.AddXunit(Xunit.TestContext.Current.TestOutputHelper);
        });

        builder.ConfigureServices(services =>
        {
            // OpenFeed evaluates DisableSampleDataSeeder while its top-level Program is being
            // registered, before WebApplicationFactory's configuration overlay is applied.
            // Remove that production sample seeder so only the deterministic test seeder runs.
            var sampleSeeder = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(PackageSeederHostedService));
            if (sampleSeeder is not null)
            {
                services.Remove(sampleSeeder);
            }

            services.Configure<NuGetSearchServiceOptions>(x => x.ServiceIndexUrl = "https://api.nuget.org/v3/index.json");

            // Ensure schema exists before seeding (OpenFeed only migrates in DEBUG Development)
            services.AddHostedService<TestDatabaseInitializer>();
            services.AddHostedService<TestPackageSeeder>();
        });
    }
}

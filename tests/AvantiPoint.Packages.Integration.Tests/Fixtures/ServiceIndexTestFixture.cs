using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Integration.Tests.Fixtures;

public class ServiceIndexTestFixture : IDisposable
{
    private readonly List<SqliteConnection> _connections = new();

    public HttpClient CreateClient(bool vulnerabilityEnabled = true, bool hasVulnerabilityData = true, bool privateFeed = false)
    {
        var factory = CreateWebApplicationFactory(vulnerabilityEnabled, hasVulnerabilityData, privateFeed);
        return factory.CreateClient();
    }

    private WebApplicationFactory<IntegrationTestApi.Program> CreateWebApplicationFactory(bool vulnerabilityEnabled, bool hasVulnerabilityData, bool privateFeed)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        return new WebApplicationFactory<IntegrationTestApi.Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        { "Database:Type", "Sqlite" },
                        { "ConnectionStrings:Sqlite", "DataSource=:memory:" },
                        { "Storage:Type", "FileSystem" },
                        { "Storage:Path", Path.Combine(Path.GetTempPath(), $"ServiceIndexTests-{Guid.NewGuid()}") }
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Remove any existing DbContext configurations
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(DbContextOptions<SqliteContext>));
                    if (descriptor != null)
                    {
                        services.Remove(descriptor);
                    }

                    // Add in-memory SQLite database
                    services.AddDbContext<SqliteContext>(options =>
                    {
                        options.UseSqlite(connection);
                    });

                    if (privateFeed)
                    {
                        services.Configure<AvantiPoint.Feed.Platform.Configuration.FeedOptions>(options => options.Authentication.AllowAnonymousPull = false);
                        services.AddSingleton<IPackageAuthenticationService, PrivateFeedAuthentication>();
                    }

                    // Configure vulnerability support
                    services.Configure<PackageFeedOptions>(options =>
                    {
                        options.EnableVulnerabilityInfo = vulnerabilityEnabled;
                    });
                });

                builder.ConfigureServices(services =>
                {
                    // Ensure database is created
                    var sp = services.BuildServiceProvider();
                    using var scope = sp.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IContext>();
                    db.Database.EnsureCreated();
                    if (hasVulnerabilityData)
                    {
                        db.PackageVulnerabilities.Add(new PackageVulnerability
                        {
                            PackageId = "test.package",
                            VersionRange = "[1.0.0,2.0.0)",
                            Vulnerability = new VulnerabilityRecord
                            {
                                AdvisoryUrl = "https://example.test/advisories/synthetic",
                                Severity = "High",
                                Description = "Synthetic vulnerability fixture",
                                CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                                UpdatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                            },
                        });
                        ((DbContext)db).SaveChanges();
                    }
                });
            });
    }

    private sealed class PrivateFeedAuthentication : IPackageAuthenticationService
    {
        public Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken) =>
            Task.FromResult(apiKey == "synthetic-vulnerability-reader" ? NuGetAuthenticationResult.Success() : NuGetAuthenticationResult.Fail("Private", "test"));
        public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken) => AuthenticateAsync(token, cancellationToken);
    }

    public void Dispose()
    {
        foreach (var connection in _connections)
        {
            connection.Close();
            connection.Dispose();
        }
    }
}

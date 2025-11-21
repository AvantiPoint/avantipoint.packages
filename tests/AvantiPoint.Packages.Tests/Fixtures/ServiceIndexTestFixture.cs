using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Tests.Fixtures;

public class ServiceIndexTestFixture : IDisposable
{
    private readonly List<SqliteConnection> _connections = new();

    public HttpClient CreateClient(bool vulnerabilityEnabled = true)
    {
        var factory = CreateWebApplicationFactory(vulnerabilityEnabled);
        return factory.CreateClient();
    }

    private WebApplicationFactory<IntegrationTestApi.Program> CreateWebApplicationFactory(bool vulnerabilityEnabled)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);

        return new WebApplicationFactory<IntegrationTestApi.Program>()
            .WithWebHostBuilder(builder =>
            {
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
                });
            });
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

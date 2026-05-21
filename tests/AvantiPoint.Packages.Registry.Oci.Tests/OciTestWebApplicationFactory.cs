using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciTestWebApplicationFactory : WebApplicationFactory<IntegrationTestApi.Program>
{
    private bool _migrated;
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"oci-test-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_tempDirectory);
        var dbPath = Path.Combine(_tempDirectory, "test.db");
        var packagesPath = Path.Combine(_tempDirectory, "packages");
        Directory.CreateDirectory(packagesPath);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = "integration-test-key",
                ["Feed:Authentication:ApiKey"] = "integration-test-key",
                ["Feed:Authentication:AllowAnonymousPull"] = "true",
                ["Database:Type"] = "Sqlite",
                ["Search:Type"] = "Database",
                ["Storage:Type"] = "FileSystem",
                ["Storage:Path"] = packagesPath,
                ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath}",
            });
        });

        builder.UseEnvironment(Environments.Development);
    }

    public async Task EnsureDatabaseMigratedAsync()
    {
        if (_migrated)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        await context.RunMigrationsAsync(CancellationToken.None);
        _migrated = true;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Best-effort cleanup for temp test files.
            }
        }
    }
}

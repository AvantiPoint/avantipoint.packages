using System.Net;
using System.Net.Sockets;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AvantiPoint.Packages.Protocol.Tests.Infrastructure;

/// <summary>
/// In-process NuGet test server host for protocol-level integration tests.
/// Creates an isolated server instance with in-memory or temp storage per test.
/// </summary>
public sealed class NuGetTestServerHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _tempDirectory;
    private bool _disposed;

    private NuGetTestServerHost(WebApplication app, string tempDirectory)
    {
        _app = app;
        _tempDirectory = tempDirectory;
        
        // Extract the actual server address after the server starts
        var server = app.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        
        if (addressFeature?.Addresses.FirstOrDefault() is string address)
        {
            BaseAddress = new Uri(address);
        }
        else
        {
            throw new InvalidOperationException("Failed to determine server address");
        }

        Client = new HttpClient { BaseAddress = BaseAddress };
    }

    /// <summary>
    /// Base URL of the running test server.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    /// HttpClient configured to communicate with the test server.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// Starts a new in-process NuGet server instance for testing.
    /// </summary>
    /// <param name="configure">Optional action to customize server configuration.</param>
    /// <returns>A running test server instance.</returns>
    public static async Task<NuGetTestServerHost> StartAsync(Action<IConfigurationBuilder>? configure = null)
    {
        // Create a unique temp directory for this test server instance
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"nuget-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var dbPath = Path.Combine(tempDirectory, "test.db");
        var packagesPath = Path.Combine(tempDirectory, "packages");
        Directory.CreateDirectory(packagesPath);

        // Find a free port
        var port = GetFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        // Configure settings for test server
        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = "test-api-key-12345",
                ["PackageDeletionBehavior"] = "HardDelete",
                ["AllowPackageOverwrites"] = "true",
                ["IsReadOnlyMode"] = "false",
                ["EnablePackageMetadataBackfill"] = "false",
                ["Database:Type"] = "Sqlite",
                ["Search:Type"] = "Database",
                ["Storage:Type"] = "FileSystem",
                ["Storage:Path"] = packagesPath,
                ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath}",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft"] = "Warning",
                ["Logging:LogLevel:Microsoft.Hosting.Lifetime"] = "Warning",
            });

        configure?.Invoke(configBuilder);

        builder.Configuration.AddConfiguration(configBuilder.Build());

        // Configure Kestrel to listen on the free port
        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, port);
        });

        // Register NuGet services
        builder.Services.AddNuGetPackageApi(options =>
        {
            options.AddFileStorage();
            options.AddSqliteDatabase("Sqlite");
        });

        var app = builder.Build();

        // Apply minimal middleware for testing
        app.UseRouting();
        app.UseOperationCancelledMiddleware();
        app.MapNuGetApiRoutes();

        // Ensure database is created
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            await context.Database.EnsureCreatedAsync();
        }

        // Start the server
        await app.StartAsync();

        return new NuGetTestServerHost(app, tempDirectory);
    }

    /// <summary>
    /// Finds an available TCP port.
    /// </summary>
    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        Client?.Dispose();

        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        // Clean up temp directory
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup - ignore errors
            }
        }
    }
}

using System.Net;
using System.Net.Sockets;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Feed.Platform.Health;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.Registry.Npm.Extensions;
using AvantiPoint.Packages.Registry.Oci.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AvantiPoint.Packages.Registry.Tests.Shared;

/// <summary>
/// In-process feed host exposing NuGet, npm, and OCI surfaces on a real HTTP port for native CLI tests.
/// </summary>
public sealed class FeedTestServerHost : IAsyncDisposable
{
    public const string DefaultApiKey = "integration-test-key";

    private readonly WebApplication _app;
    private readonly string _tempDirectory;
    private bool _disposed;

    private FeedTestServerHost(WebApplication app, string tempDirectory)
    {
        _app = app;
        _tempDirectory = tempDirectory;

        var server = app.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        if (addressFeature?.Addresses.FirstOrDefault() is not string address)
        {
            throw new InvalidOperationException("Failed to determine server address.");
        }

        BaseAddress = new Uri(address);
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        Client = new HttpClient(handler) { BaseAddress = BaseAddress };
    }

    public Uri BaseAddress { get; }

    public HttpClient Client { get; }

    public Uri NpmRegistryUrl => new(BaseAddress, "npm/");

    public Uri OciRegistryUrl => BaseAddress;

    /// <summary>
    /// Registry host for the Docker CLI. Uses host.docker.internal on Windows/macOS so the daemon can reach the in-process feed.
    /// </summary>
    public string DockerRegistryHost => GetDockerRegistryHost(BaseAddress);

    private static string GetDockerRegistryHost(Uri baseAddress)
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            return $"host.docker.internal:{baseAddress.Port}";
        }

        return baseAddress.Authority;
    }

    public static async Task<FeedTestServerHost> StartAsync(
        string apiKey = DefaultApiKey,
        Action<IConfigurationBuilder>? configure = null)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"feed-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var dbPath = Path.Combine(tempDirectory, "test.db");
        var packagesPath = Path.Combine(tempDirectory, "packages");
        Directory.CreateDirectory(packagesPath);

        var port = GetFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        var configBuilder = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiKey"] = apiKey,
                ["Feed:Authentication:ApiKey"] = apiKey,
                ["Feed:Authentication:AllowAnonymousPull"] = "true",
                ["PackageDeletionBehavior"] = "HardDelete",
                ["AllowPackageOverwrites"] = "true",
                ["IsReadOnlyMode"] = "false",
                ["Database:Type"] = "Sqlite",
                ["Search:Type"] = "Database",
                ["Storage:Type"] = "FileSystem",
                ["Storage:Path"] = packagesPath,
                ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath}",
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft"] = "Warning",
            });

        configure?.Invoke(configBuilder);
        builder.Configuration.AddConfiguration(configBuilder.Build());

        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, port));

        builder.Services.AddNuGetPackageApi(options =>
        {
            options.AddFileStorage();
            options.AddSqliteDatabase("Sqlite");
        });

        var feed = builder.AddAvantiPointFeed(builder.Configuration.GetSection("Feed"));
        feed.UseNuGet();
        feed.UseNpmRegistry();
        feed.UseOciRegistry();
        feed.UseOciRegistry("docker");

        var app = builder.Build();
        app.UseAvantiPointFeedPlatform();
        app.UseRouting();
        app.MapNuGetApiRoutes();
        app.MapNpmFeed(feed);
        app.MapOciFeed(feed);
        app.MapFeedHealthEndpoints();

        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            await context.RunMigrationsAsync(CancellationToken.None);
        }

        await app.StartAsync();
        return new FeedTestServerHost(app, tempDirectory);
    }

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
        {
            return;
        }

        _disposed = true;
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();

        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}

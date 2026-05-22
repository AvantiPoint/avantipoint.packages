using System.Net;
using System.Net.Sockets;
using AvantiPoint.Feed.Platform.Extensions;
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

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// Kestrel-hosted upstream feed using the same OpenFeed sample stack (NuGet API + file storage + Sqlite).
/// Listens on loopback so downstream feeds can mirror via real HTTP (required by <see cref="MirrorService"/>).
/// </summary>
public sealed class UpstreamOpenFeedHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _tempDirectory;
    private bool _disposed;

    private UpstreamOpenFeedHost(WebApplication app, string tempDirectory, HttpClient client, Uri baseAddress)
    {
        _app = app;
        _tempDirectory = tempDirectory;
        Client = client;
        BaseAddress = baseAddress;
        StoragePath = Path.Combine(tempDirectory, "packages");
    }

    public HttpClient Client { get; }

    public Uri BaseAddress { get; }

    public string ServiceIndexUrl => new Uri(BaseAddress, "/v3/index.json").ToString();

    public string StoragePath { get; }

    public static async Task<UpstreamOpenFeedHost> StartAsync(
        string apiKey = TestPackageBuilder.DefaultApiKey,
        CancellationToken cancellationToken = default)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"openfeed-upstream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var dbPath = Path.Combine(tempDirectory, "upstream.db");
        var packagesPath = Path.Combine(tempDirectory, "packages");
        Directory.CreateDirectory(packagesPath);

        var port = GetFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DisableSampleDataSeeder"] = "true",
            ["ApiKey"] = apiKey,
            ["Feed:Authentication:ApiKey"] = apiKey,
            ["Feed:Authentication:AllowAnonymousPull"] = "true",
            ["PackageDeletionBehavior"] = "HardDelete",
            ["AllowPackageOverwrites"] = "true",
            ["IsReadOnlyMode"] = "false",
            ["EnablePackageMetadataBackfill"] = "false",
            ["Database:Type"] = "Sqlite",
            ["Search:Type"] = "Database",
            ["Storage:Type"] = "FileSystem",
            ["Storage:Path"] = packagesPath,
            ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath}",
            ["Signing:Provider"] = null,
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft"] = "Warning",
        });

        builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, port));

        builder.Services.AddNuGetPackageApi(options =>
        {
            options.AddFileStorage();
            options.AddSqliteDatabase("Sqlite");
        });

        var feed = builder.AddAvantiPointFeed(builder.Configuration.GetSection("Feed"));
        feed.UseNuGet();

        var app = builder.Build();
        app.UseAvantiPointFeedPlatform();
        app.UseRouting();
        app.MapNuGetApiRoutes();

        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            await context.RunMigrationsAsync(cancellationToken);
        }

        await app.StartAsync(cancellationToken);

        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Failed to determine upstream server address.");

        var baseAddress = new Uri(address);
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler) { BaseAddress = baseAddress };

        return new UpstreamOpenFeedHost(app, tempDirectory, client, baseAddress);
    }

    public async Task SeedPackageAsync(string packageId, string version, CancellationToken cancellationToken = default)
    {
        await TestPackageBuilder.PublishAsync(Client, packageId, version, cancellationToken: cancellationToken);
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
            // Best effort cleanup
        }
    }
}

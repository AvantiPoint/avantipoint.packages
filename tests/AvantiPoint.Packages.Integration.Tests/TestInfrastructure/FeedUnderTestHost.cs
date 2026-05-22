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
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Integration.Tests.TestInfrastructure;

/// <summary>
/// Kestrel-hosted feed under test (IntegrationTestApi stack) with optional upstream <see cref="PackageSource"/>.
/// </summary>
public sealed class FeedUnderTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _tempDirectory;
    private bool _disposed;

    private FeedUnderTestHost(
        WebApplication app,
        string tempDirectory,
        HttpClient client,
        Uri baseAddress,
        string storagePath,
        string dbPath)
    {
        _app = app;
        _tempDirectory = tempDirectory;
        Client = client;
        BaseAddress = baseAddress;
        StoragePath = storagePath;
        DbPath = dbPath;
    }

    public HttpClient Client { get; }

    public Uri BaseAddress { get; }

    public string StoragePath { get; }

    public string DbPath { get; }

    public IServiceProvider Services => _app.Services;

    public static async Task<FeedUnderTestHost> StartAsync(
        FeedUnderTestOptions options,
        CancellationToken cancellationToken = default)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"feed-under-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var dbPath = Path.Combine(tempDirectory, "feed.db");
        var packagesPath = Path.Combine(tempDirectory, "packages");
        Directory.CreateDirectory(packagesPath);

        var port = GetFreePort();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        var settings = new Dictionary<string, string?>
        {
            ["ApiKey"] = options.ApiKey,
            ["Feed:Authentication:ApiKey"] = options.ApiKey,
            ["Feed:Authentication:AllowAnonymousPull"] = "true",
            ["PackageDeletionBehavior"] = "HardDelete",
            ["AllowPackageOverwrites"] = "true",
            ["IsReadOnlyMode"] = "false",
            ["EnablePackageMetadataBackfill"] = "false",
            ["Database:Type"] = "Sqlite",
            ["Search:Type"] = "Database",
            ["Search:IncludeMirroredPackages"] = options.IncludeMirroredPackages ? "true" : "false",
            ["Storage:Type"] = "FileSystem",
            ["Storage:Path"] = packagesPath,
            ["ConnectionStrings:Sqlite"] = $"Data Source={dbPath}",
            ["Signing:Provider"] = null,
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft"] = "Warning",
        };

        builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseKestrel(o => o.Listen(IPAddress.Loopback, port));

        builder.Services.AddNuGetPackageApi(o =>
        {
            o.AddFileStorage();
            o.AddSqliteDatabase("Sqlite");
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

            if (options.UpstreamServiceIndexUrl is not null)
            {
                context.PackageSources.Add(new PackageSource
                {
                    Name = options.UpstreamSourceName ?? "test-upstream",
                    FeedUrl = options.UpstreamServiceIndexUrl,
                    Type = PackageSourceType.Upstream,
                    CachingStrategy = options.CachingStrategy,
                    IsEnabled = true,
                });
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        await app.StartAsync(cancellationToken);

        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault()
            ?? throw new InvalidOperationException("Failed to determine feed-under-test server address.");

        var baseAddress = new Uri(address);
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler) { BaseAddress = baseAddress };

        return new FeedUnderTestHost(app, tempDirectory, client, baseAddress, packagesPath, dbPath);
    }

    public async Task<Package?> FindPackageAsync(string id, string version, CancellationToken cancellationToken = default)
    {
        using var scope = Services.CreateScope();
        var packages = scope.ServiceProvider.GetRequiredService<IPackageService>();
        return await packages.FindOrNullAsync(
            id,
            NuGet.Versioning.NuGetVersion.Parse(version),
            includeUnlisted: true,
            cancellationToken);
    }

    public static int CountPackageFiles(string storagePath) =>
        Directory.Exists(storagePath)
            ? Directory.GetFiles(storagePath, "*.nupkg", SearchOption.AllDirectories).Length
            : 0;

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

public sealed class FeedUnderTestOptions
{
    public string ApiKey { get; init; } = TestPackageBuilder.DefaultApiKey;

    public bool IncludeMirroredPackages { get; init; } = true;

    public string? UpstreamServiceIndexUrl { get; init; }

    public string? UpstreamSourceName { get; init; }

    public PackageSourceCachingStrategy CachingStrategy { get; init; } =
        PackageSourceCachingStrategy.IndexAndCache;
}

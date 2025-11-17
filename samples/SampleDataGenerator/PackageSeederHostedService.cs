using System;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace SampleDataGenerator;

/// <summary>
/// Hosted service that seeds the feed with sample packages from NuGet.org if the database is empty
/// </summary>
public class PackageSeederHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PackageSeederHostedService> _logger;
    private readonly SampleDataSeederOptions _options;

    public PackageSeederHostedService(
        IServiceProvider serviceProvider,
        ILogger<PackageSeederHostedService> logger,
        SampleDataSeederOptions options)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Sample data seeder is disabled.");
            return;
        }

        _logger.LogInformation("Checking if database needs seeding...");

        // Create a scope to access scoped services
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var indexingService = scope.ServiceProvider.GetRequiredService<IPackageIndexingService>();

        // Check if database already has packages
        var hasPackages = context.Packages.Any();
        if (hasPackages)
        {
            _logger.LogInformation("Database already contains packages. Skipping seed.");
            return;
        }

        _logger.LogInformation("Database is empty. Starting package seeding from NuGet.org...");

        var nugetClient = new NuGetClient("https://api.nuget.org/v3/index.json");
        var totalDownloaded = 0;
        var totalFailed = 0;

        foreach (var packageDef in SamplePackages.Packages)
        {
            try
            {
                _logger.LogInformation("Processing package: {PackageId}", packageDef.PackageId);

                // Get all versions of the package
                var versions = await nugetClient.ListPackageVersionsAsync(
                    packageDef.PackageId,
                    includeUnlisted: false,
                    cancellationToken);

                if (!versions.Any())
                {
                    _logger.LogWarning("No versions found for package: {PackageId}", packageDef.PackageId);
                    continue;
                }

                // Filter prerelease if needed
                var filteredVersions = packageDef.IncludePrerelease
                    ? versions
                    : versions.Where(v => !v.IsPrerelease).ToList();

                // Take the latest N versions
                var versionsToDownload = filteredVersions
                    .OrderByDescending(v => v)
                    .Take(packageDef.MaxVersions)
                    .ToList();

                _logger.LogInformation("Found {Count} versions to download for {PackageId}",
                    versionsToDownload.Count, packageDef.PackageId);

                foreach (var version in versionsToDownload)
                {
                    if (await DownloadAndIndexPackageAsync(indexingService, nugetClient, packageDef.PackageId, version, cancellationToken))
                    {
                        totalDownloaded++;
                    }
                    else
                    {
                        totalFailed++;
                    }

                    // Add a small delay to avoid overwhelming the server
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process package: {PackageId}", packageDef.PackageId);
                totalFailed++;
            }
        }

        _logger.LogInformation(
            "Package seeding completed. Downloaded: {Downloaded}, Failed: {Failed}",
            totalDownloaded, totalFailed);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Package seeder service stopping...");
        return Task.CompletedTask;
    }

    private async Task<bool> DownloadAndIndexPackageAsync(
        IPackageIndexingService indexingService,
        NuGetClient client,
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Downloading {PackageId} {Version}...", packageId, version);

            // Download the package
            using var packageStream = await client.DownloadPackageAsync(packageId, version, cancellationToken);

            // Create a memory stream to hold the package content
            // (indexing service may need seekable stream)
            using var memoryStream = new MemoryStream();
            await packageStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            // Index the package
            var result = await indexingService.IndexAsync(memoryStream, cancellationToken);

            switch (result.Status)
            {
                case PackageIndexingStatus.Success:
                    _logger.LogInformation("Successfully indexed {PackageId} {Version}",
                        packageId, version);
                    return true;

                case PackageIndexingStatus.PackageAlreadyExists:
                    _logger.LogInformation("Package {PackageId} {Version} already exists",
                        packageId, version);
                    return true;

                case PackageIndexingStatus.InvalidPackage:
                    _logger.LogWarning("Package {PackageId} {Version} is invalid",
                        packageId, version);
                    return false;

                default:
                    _logger.LogWarning("Unknown indexing status for {PackageId} {Version}: {Status}",
                        packageId, version, result.Status);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download/index {PackageId} {Version}",
                packageId, version);
            return false;
        }
    }
}

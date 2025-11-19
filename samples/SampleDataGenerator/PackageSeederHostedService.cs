using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace SampleDataGenerator;

/// <summary>
/// Hosted service that seeds the feed with sample packages from NuGet.org in the background.
/// </summary>
public sealed class PackageSeederHostedService(
    IServiceProvider serviceProvider,
    ILogger<PackageSeederHostedService> logger,
    SampleDataSeederOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Sample data seeder is disabled.");
            return;
        }

        logger.LogInformation("Starting sample data seeder in the background...");

        var downloadChannel = Channel.CreateUnbounded<DownloadSeedRequest>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _ = Task.Run(async () =>
        {
            var producerTask = RunSeedingAsync(downloadChannel.Writer, stoppingToken);
            var consumerTask = ProcessDownloadQueueAsync(downloadChannel.Reader, stoppingToken);
            await Task.WhenAll(producerTask, consumerTask);
        }, stoppingToken);

        await Task.CompletedTask;
    }

    private async Task RunSeedingAsync(ChannelWriter<DownloadSeedRequest> downloadWriter, CancellationToken cancellationToken)
    {
        var nugetClient = new NuGetClient("https://api.nuget.org/v3/index.json");

        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        var indexingService = scope.ServiceProvider.GetRequiredService<IPackageIndexingService>();

        try
        {
            foreach (var packageDef in SamplePackages.Packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await EnsurePackageSeededAsync(context, indexingService, nugetClient, packageDef, downloadWriter, cancellationToken);
            }
        }
        finally
        {
            downloadWriter.TryComplete();
        }
    }

    private async Task EnsurePackageSeededAsync(
        IContext context,
        IPackageIndexingService indexingService,
        NuGetClient nugetClient,
        PackageDefinition packageDefinition,
        ChannelWriter<DownloadSeedRequest> downloadWriter,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Synchronizing package: {PackageId}", packageDefinition.PackageId);

            var downloadCounts = await GetDownloadCountsAsync(
                nugetClient,
                packageDefinition.PackageId,
                packageDefinition.IncludePrerelease,
                cancellationToken);

            var versions = await nugetClient.ListPackageVersionsAsync(
                packageDefinition.PackageId,
                includeUnlisted: false,
                cancellationToken);

            if (!versions.Any())
            {
                logger.LogWarning("No versions found for {PackageId}", packageDefinition.PackageId);
                return;
            }

            var filteredVersions = packageDefinition.IncludePrerelease
                ? versions
                : [.. versions.Where(v => !v.IsPrerelease)];

            var versionsToProcess = filteredVersions
                .OrderByDescending(v => v)
                .Take(packageDefinition.MaxVersions)
                .ToList();

            var existingPackages = await context.Packages
                .Where(p => p.Id == packageDefinition.PackageId)
                .Select(p => new { p.Key, p.NormalizedVersionString })
                .ToListAsync(cancellationToken);

            var packageMap = existingPackages.ToDictionary(
                p => p.NormalizedVersionString,
                p => p.Key,
                StringComparer.Ordinal);

            foreach (var version in versionsToProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalized = version.ToNormalizedString().ToLowerInvariant();
                var packageKey = await EnsurePackageVersionAsync(
                    context,
                    indexingService,
                    nugetClient,
                    packageDefinition.PackageId,
                    version,
                    normalized,
                    packageMap,
                    cancellationToken);

                if (!packageKey.HasValue)
                {
                    continue;
                }

                if (!downloadCounts.TryGetValue(version.ToNormalizedString(), out var downloadCount) &&
                    !downloadCounts.TryGetValue(normalized, out downloadCount))
                {
                    logger.LogDebug("Skipping downloads for {PackageId} {Version} - missing counts", packageDefinition.PackageId, version);
                    continue;
                }

                if (downloadCount <= 0)
                {
                    continue;
                }

                await downloadWriter.WriteAsync(
                    new DownloadSeedRequest(packageKey.Value, packageDefinition.PackageId, normalized, downloadCount),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to synchronize package: {PackageId}", packageDefinition.PackageId);
        }
    }

    private async Task<int?> EnsurePackageVersionAsync(
        IContext context,
        IPackageIndexingService indexingService,
        NuGetClient nugetClient,
        string packageId,
        NuGetVersion version,
        string normalizedVersion,
        IDictionary<string, int> packageMap,
        CancellationToken cancellationToken)
    {
        if (!packageMap.TryGetValue(normalizedVersion, out var packageKey))
        {
            var indexed = await DownloadAndIndexPackageAsync(indexingService, nugetClient, packageId, version, cancellationToken);
            if (!indexed)
            {
                return null;
            }

            packageKey = await context.Packages
                .Where(p => p.Id == packageId && p.NormalizedVersionString == normalizedVersion)
                .Select(p => (int?)p.Key)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            if (packageKey == 0)
            {
                logger.LogWarning("Package {PackageId} {Version} was indexed but not found in the database.", packageId, version);
                return null;
            }

            packageMap[normalizedVersion] = packageKey;
        }

        return packageKey;
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
            logger.LogInformation("Downloading {PackageId} {Version}...", packageId, version);

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
                    logger.LogInformation("Successfully indexed {PackageId} {Version}",
                        packageId, version);
                    return true;

                case PackageIndexingStatus.PackageAlreadyExists:
                    logger.LogInformation("Package {PackageId} {Version} already exists",
                        packageId, version);
                    return true;

                case PackageIndexingStatus.InvalidPackage:
                    logger.LogWarning("Package {PackageId} {Version} is invalid",
                        packageId, version);
                    return false;

                default:
                    logger.LogWarning("Unknown indexing status for {PackageId} {Version}: {Status}",
                        packageId, version, result.Status);
                    return false;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download/index {PackageId} {Version}",
                packageId, version);
            return false;
        }
    }

    private async Task ProcessDownloadQueueAsync(
        ChannelReader<DownloadSeedRequest> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var request in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IContext>();
                await CreateSyntheticDownloadsAsync(context, request, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate downloads for {PackageId} {Version}", request.PackageId, request.NormalizedVersion);
            }
        }
    }

    private static async Task CreateSyntheticDownloadsAsync(
        IContext context,
        DownloadSeedRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DownloadCount <= 0)
        {
            return;
        }

        var existingCount = await context.PackageDownloads
            .Where(d => d.PackageKey == request.PackageKey)
            .LongCountAsync(cancellationToken);

        var remaining = Math.Min(request.DownloadCount, 5_000_000) - existingCount;
        if (remaining <= 0)
        {
            return;
        }
        const int BatchSize = 10_000;

        while (remaining > 0)
        {
            var currentBatchSize = (int)Math.Min(remaining, BatchSize);
            var downloads = SampleDownloadFactory.CreateDownloads(request.PackageKey, currentBatchSize);
            await context.PackageDownloads.AddRangeAsync(downloads, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            remaining -= currentBatchSize;
        }
    }

    private async Task<Dictionary<string, long>> GetDownloadCountsAsync(
        NuGetClient nugetClient,
        string packageId,
        bool includePrerelease,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var searchResults = await nugetClient.SearchAsync(
                query: $"packageid:{packageId}",
                skip: 0,
                take: 1,
                includePrerelease: true,
                cancellationToken: cancellationToken);

            var match = searchResults
                .FirstOrDefault(r => string.Equals(r.PackageId, packageId, StringComparison.OrdinalIgnoreCase));

            if (match?.Versions is null)
            {
                return map;
            }

            foreach (var entry in match.Versions)
            {
                if (string.IsNullOrWhiteSpace(entry.Version))
                {
                    continue;
                }

                if (!NuGetVersion.TryParse(entry.Version, out var parsed))
                {
                    continue;
                }

                if (!includePrerelease && parsed.IsPrerelease)
                {
                    continue;
                }

                map[parsed.ToNormalizedString()] = entry.Downloads;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve download counts for {PackageId}.", packageId);
        }

        return map;
    }

    private sealed record DownloadSeedRequest(int PackageKey, string PackageId, string NormalizedVersion, long DownloadCount);
}

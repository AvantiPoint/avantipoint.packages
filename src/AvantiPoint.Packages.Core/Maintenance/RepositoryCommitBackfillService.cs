using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NuGet.Packaging;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Background service that backfills repository commit metadata for existing packages.
    /// </summary>
    public class RepositoryCommitBackfillService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RepositoryCommitBackfillService> _logger;
        private readonly IOptionsMonitor<PackageFeedOptions> _options;

        public RepositoryCommitBackfillService(
            IServiceProvider serviceProvider,
            ILogger<RepositoryCommitBackfillService> logger,
            IOptionsMonitor<PackageFeedOptions> options)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Wait a bit for the application to fully start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            if (!_options.CurrentValue.EnablePackageMetadataBackfill)
            {
                _logger.LogInformation("Package metadata backfill is disabled");
                return;
            }

            try
            {
                await RunBackfillAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Repository commit backfill cancelled during shutdown");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error during repository commit backfill");
            }
        }

        private async Task RunBackfillAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var stateService = scope.ServiceProvider.GetRequiredService<IPackageBackfillStateService>();
            var storage = scope.ServiceProvider.GetRequiredService<IPackageStorageService>();
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            
            var state = await stateService.GetStateAsync(cancellationToken);

            // Check if the repository commit backfill has already been completed
            if (state.RepositoryCommitBackfill?.IsCompleted == true)
            {
                _logger.LogInformation(
                    "Repository commit backfill already completed on {CompletedTime}. Processed {PackagesProcessed} packages, updated {PackagesUpdated}.",
                    state.RepositoryCommitBackfill.CompletedTime,
                    state.RepositoryCommitBackfill.PackagesProcessed,
                    state.RepositoryCommitBackfill.PackagesUpdated);
                return;
            }

            _logger.LogInformation("Starting repository commit backfill");

            var operationInfo = new BackfillOperationInfo
            {
                LastRunTime = DateTimeOffset.UtcNow,
                PackagesProcessed = 0,
                PackagesUpdated = 0
            };

            try
            {
                // Query packages that need backfill: RepositoryCommit is null but RepositoryUrl is not null
                var packagesToBackfill = await context.Packages
                    .Where(p => p.RepositoryCommit == null && p.RepositoryUrl != null)
                    .Select(p => new PackageIdentifier { Id = p.Id, Version = p.Version })
                    .ToListAsync(cancellationToken);

                _logger.LogInformation("Found {Count} packages that need repository commit backfill", packagesToBackfill.Count);

                const int batchSize = 50;
                for (int i = 0; i < packagesToBackfill.Count; i += batchSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var batch = packagesToBackfill.Skip(i).Take(batchSize).ToList();
                    await ProcessBatchAsync(batch, storage, context, operationInfo, cancellationToken);

                    // Save progress after each batch
                    state.RepositoryCommitBackfill = operationInfo;
                    await stateService.SaveStateAsync(state, cancellationToken);

                    // Small delay between batches to avoid overwhelming the system
                    await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                }

                // Mark as completed
                operationInfo.IsCompleted = true;
                operationInfo.CompletedTime = DateTimeOffset.UtcNow;
                state.RepositoryCommitBackfill = operationInfo;
                await stateService.SaveStateAsync(state, cancellationToken);

                _logger.LogInformation(
                    "Repository commit backfill completed. Processed {PackagesProcessed} packages, updated {PackagesUpdated}.",
                    operationInfo.PackagesProcessed,
                    operationInfo.PackagesUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during repository commit backfill");
                operationInfo.LastError = ex.Message;
                state.RepositoryCommitBackfill = operationInfo;
                await stateService.SaveStateAsync(state, cancellationToken);
                throw;
            }
        }

        private async Task ProcessBatchAsync(
            List<PackageIdentifier> batch,
            IPackageStorageService storage,
            IContext context,
            BackfillOperationInfo operationInfo,
            CancellationToken cancellationToken)
        {
            foreach (var pkg in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                operationInfo.PackagesProcessed++;

                try
                {
                    await ProcessPackageAsync(pkg.Id, pkg.Version, storage, context, operationInfo, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to backfill repository commit for package {Id} {Version}", pkg.Id, pkg.Version.ToString());
                }
            }
        }

        private async Task ProcessPackageAsync(
            string id,
            NuGet.Versioning.NuGetVersion version,
            IPackageStorageService storage,
            IContext context,
            BackfillOperationInfo operationInfo,
            CancellationToken cancellationToken)
        {
            try
            {
                // Get the package stream from storage
                using var packageStream = await storage.GetPackageStreamAsync(id, version, cancellationToken);
                if (packageStream == null)
                {
                    _logger.LogWarning("Package stream not found for {Id} {Version}", id, version);
                    return;
                }

                // Parse the package to extract repository metadata
                using var packageReader = new PackageArchiveReader(packageStream, leaveStreamOpen: false);
                var nuspec = packageReader.NuspecReader;
                var repository = nuspec.GetRepositoryMetadata();

                if (repository == null || string.IsNullOrWhiteSpace(repository.Commit))
                {
                    _logger.LogDebug("No repository commit found in package {Id} {Version}", id, version);
                    return;
                }

                // Validate commit SHA length
                var commit = repository.Commit;
                if (commit.Length > 64)
                {
                    _logger.LogWarning("Repository commit SHA too long for package {Id} {Version}", id, version);
                    return;
                }

                // Update the package in the database
                var package = await context.Packages
                    .Where(p => p.Id == id && p.NormalizedVersionString == version.ToNormalizedString())
                    .FirstOrDefaultAsync(cancellationToken);

                if (package == null)
                {
                    _logger.LogWarning("Package not found in database: {Id} {Version}", id, version);
                    return;
                }

                // Only update if the commit is not already set
                if (string.IsNullOrWhiteSpace(package.RepositoryCommit))
                {
                    package.RepositoryCommit = commit;
                    // Note: RepositoryCommitDate is not available in the nuspec, so we leave it as null
                    
                    await context.SaveChangesAsync(cancellationToken);
                    operationInfo.PackagesUpdated++;

                    _logger.LogDebug("Updated repository commit for package {Id} {Version}: {Commit}", id, version, commit);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing package {Id} {Version}", id, version);
                throw;
            }
        }
    }

    /// <summary>
    /// Simple package identifier for backfill operations.
    /// </summary>
    internal class PackageIdentifier
    {
        public string Id { get; set; }
        public NuGet.Versioning.NuGetVersion Version { get; set; }
    }
}

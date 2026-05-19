using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core;

public class SearchIndexReconciliationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SearchOptions> _searchOptions;
    private readonly ILogger<SearchIndexReconciliationHostedService> _logger;

    public SearchIndexReconciliationHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<SearchOptions> searchOptions,
        ILogger<SearchIndexReconciliationHostedService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _searchOptions = searchOptions ?? throw new ArgumentNullException(nameof(searchOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var indexer = scope.ServiceProvider.GetRequiredService<ISearchIndexer>();

            if (indexer is NullSearchIndexer)
            {
                _logger.LogDebug("Search reconciliation skipped: {IndexerKey} indexer is in use.", indexer.Key);
                return;
            }

            await ReconcileAsync(scope.ServiceProvider, indexer, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Search index reconciliation failed.");
        }
    }

    private async Task ReconcileAsync(IServiceProvider services, ISearchIndexer indexer, CancellationToken cancellationToken)
    {
        var context = services.GetRequiredService<IContext>();
        var indexing = services.GetRequiredService<ISearchIndexingService>();
        var batchSize = Math.Max(1, _searchOptions.Value.ReconcileBatchSize);
        var indexerKey = indexer.Key;

        var state = await context.SearchIndexStates
            .FindAsync([SearchIndexState.SingletonId], cancellationToken);

        if (state == null)
        {
            state = new SearchIndexState
            {
                Id = SearchIndexState.SingletonId,
                SchemaVersion = SearchIndexerKeys.CurrentSchemaVersion,
            };
            context.SearchIndexStates.Add(state);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (state.SchemaVersion != SearchIndexerKeys.CurrentSchemaVersion)
        {
            _logger.LogInformation(
                "Search schema version changed ({Old} -> {New}). Clearing IndexedWith for full reindex.",
                state.SchemaVersion,
                SearchIndexerKeys.CurrentSchemaVersion);

            await context.Packages.ExecuteUpdateAsync(
                s => s.SetProperty(p => p.IndexedWith, (string)null),
                cancellationToken);

            state.SchemaVersion = SearchIndexerKeys.CurrentSchemaVersion;
        }

        state.ReconcileInProgress = true;
        context.SearchIndexStates.Update(state);
        await context.SaveChangesAsync(cancellationToken);

        var packageIds = await context.Packages
            .AsNoTracking()
            .Where(p => p.Listed)
            .Where(p => p.IndexedWith == null || p.IndexedWith != indexerKey)
            .Select(p => p.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (packageIds.Count == 0)
        {
            _logger.LogInformation("Search index is up to date for indexer {IndexerKey}.", indexerKey);
        }
        else
        {
            _logger.LogInformation(
                "Reconciling {Count} package(s) for search indexer {IndexerKey}.",
                packageIds.Count,
                indexerKey);

            var indexed = 0;
            foreach (var batch in packageIds.Chunk(batchSize))
            {
                foreach (var packageId in batch)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var package = await context.Packages
                        .AsNoTracking()
                        .Where(p => p.Id == packageId && p.Listed)
                        .OrderByDescending(p => p.Published)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (package == null)
                    {
                        continue;
                    }

                    await indexing.IndexAsync(package, cancellationToken);
                    indexed++;
                }
            }

            _logger.LogInformation(
                "Search reconciliation indexed {Indexed} package(s) for indexer {IndexerKey}.",
                indexed,
                indexerKey);
        }

        state.LastReconcileCompletedAt = DateTime.UtcNow;
        state.ReconcileInProgress = false;
        context.SearchIndexStates.Update(state);
        await context.SaveChangesAsync(cancellationToken);
    }
}

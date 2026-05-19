using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Core;

public class SearchIndexingService : ISearchIndexingService
{
    private readonly ISearchIndexer _indexer;
    private readonly IContext _context;
    private readonly ILogger<SearchIndexingService> _logger;

    public SearchIndexingService(
        ISearchIndexer indexer,
        IContext context,
        ILogger<SearchIndexingService> logger)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task IndexAsync(Package package, CancellationToken cancellationToken)
    {
        await _indexer.IndexAsync(package, cancellationToken);
        await SetIndexedWithAsync(package.Id, _indexer.Key, cancellationToken);
    }

    public async Task RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        await _indexer.RemoveAsync(packageId, cancellationToken);
        await ClearIndexedWithAsync(packageId, cancellationToken);
    }

    private async Task SetIndexedWithAsync(string packageId, string indexerKey, CancellationToken cancellationToken)
    {
        var rows = await _context.Packages
            .Where(p => p.Id == packageId)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.IndexedWith = indexerKey;
        }

        if (rows.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("Set IndexedWith={IndexerKey} for package {PackageId} ({Count} versions)", indexerKey, packageId, rows.Count);
        }
    }

    private async Task ClearIndexedWithAsync(string packageId, CancellationToken cancellationToken)
    {
        var rows = await _context.Packages
            .Where(p => p.Id == packageId)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.IndexedWith = null;
        }

        if (rows.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}

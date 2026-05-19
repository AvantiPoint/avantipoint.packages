using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// No-op indexer used when search does not use an external index (Database and Null search types).
/// </summary>
public class NullSearchIndexer : ISearchIndexer
{
    public string Key => SearchIndexerKeys.Null;

    public Task IndexAsync(Package package, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveAsync(string packageId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

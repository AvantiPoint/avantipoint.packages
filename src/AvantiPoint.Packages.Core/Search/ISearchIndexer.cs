using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

public interface ISearchIndexer
{
    /// <summary>
    /// Stable identifier for this indexer implementation (e.g. <see cref="SearchIndexerKeys.Null"/>).
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Add or update a package in the search index (all versions for the package id).
    /// </summary>
    Task IndexAsync(Package package, CancellationToken cancellationToken);

    /// <summary>
    /// Remove a package id from the search index.
    /// </summary>
    Task RemoveAsync(string packageId, CancellationToken cancellationToken);
}

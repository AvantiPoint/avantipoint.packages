using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Coordinates search indexing and updates <see cref="Package.IndexedWith"/> after success.
/// </summary>
public interface ISearchIndexingService
{
    Task IndexAsync(Package package, CancellationToken cancellationToken);

    Task RemoveAsync(string packageId, CancellationToken cancellationToken);
}

#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Extension methods for <see cref="IPackageIndexingService"/>.
/// </summary>
public static class IPackageIndexingServiceExtensions
{
    /// <summary>
    /// Attempt to index a new package with default ingestion context.
    /// </summary>
    /// <param name="indexingService">The indexing service.</param>
    /// <param name="stream">The stream containing the package's content.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the attempted indexing operation.</returns>
    public static Task<PackageIndexingResult> IndexAsync(
        this IPackageIndexingService indexingService,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return indexingService.IndexAsync(stream, context: null, cancellationToken);
    }
}


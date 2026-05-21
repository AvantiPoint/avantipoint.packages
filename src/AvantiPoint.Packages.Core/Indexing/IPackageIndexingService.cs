#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The result of attempting to index a package.
    /// See <see cref="IPackageIndexingService.IndexAsync(Stream, PackageIngestionContext?, CancellationToken)"/>.
    /// </summary>
    /// <summary>
    /// The service used to accept new packages.
    /// </summary>
    public interface IPackageIndexingService
    {
        /// <summary>
        /// Attempt to index a new package.
        /// </summary>
        /// <param name="stream">The stream containing the package's content.</param>
        /// <param name="context">Additional ingestion options (origin, signature policies, etc.). If null, defaults are used.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The result of the attempted indexing operation.</returns>
        Task<PackageIndexingResult> IndexAsync(Stream stream, PackageIngestionContext? context, CancellationToken cancellationToken);
    }
}

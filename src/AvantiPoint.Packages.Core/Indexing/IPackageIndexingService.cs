using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The result of attempting to index a package.
    /// See <see cref="IPackageIndexingService.IndexAsync(Stream, CancellationToken)"/>.
    /// </summary>
    public enum PackageIndexingStatus
    {
        /// <summary>
        /// The package is malformed. This may also happen if AvantiPoint Packages is in a corrupted state.
        /// </summary>
        InvalidPackage,

        /// <summary>
        /// The package has already been indexed.
        /// </summary>
        PackageAlreadyExists,

        /// <summary>
        /// The package has been indexed successfully.
        /// </summary>
        Success,
    }

    public record PackageIndexingResult
    {
        public string PackageId { get; init; }
        public string PackageVersion { get; init; }
        public PackageIndexingStatus Status { get; init; }
    }

    /// <summary>
    /// The service used to accept new packages.
    /// </summary>
    public interface IPackageIndexingService
    {
        /// <summary>
        /// Attempt to index a new package.
        /// </summary>
        /// <param name="stream">The stream containing the package's content.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The result of the attempted indexing operation.</returns>
        Task<PackageIndexingResult> IndexAsync(Stream stream, CancellationToken cancellationToken);
    }
}

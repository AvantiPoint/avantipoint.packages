using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Stores packages' content. Packages' state are stored by the
    /// <see cref="IPackageService"/>.
    /// </summary>
    public interface IPackageStorageService
    {
        /// <summary>
        /// Persist a package's content to storage. This operation MUST fail if a package
        /// with the same id/version but different content has already been stored.
        /// </summary>
        /// <param name="package">The package's metadata.</param>
        /// <param name="packageStream">The package's nupkg stream.</param>
        /// <param name="nuspecStream">The package's nuspec stream.</param>
        /// <param name="readmeStream">The package's readme stream, or null if none.</param>
        /// <param name="iconStream">The package's icon stream, or null if none.</param>
        /// <param name="licenseStream">The package's embedded license stream, or null if none.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SavePackageContentAsync(
            Package package,
            Stream packageStream,
            Stream nuspecStream,
            Stream readmeStream,
            Stream iconStream,
            Stream licenseStream,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve a package's nupkg stream.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The package's nupkg stream.</returns>
        Task<Stream> GetPackageStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve a package's nuspec stream.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The package's nuspec stream.</returns>
        Task<Stream> GetNuspecStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve a package's readme stream.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The package's readme stream.</returns>
        Task<Stream> GetReadmeStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        Task<Stream> GetIconStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve a package's embedded license stream.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The package's embedded license stream.</returns>
        Task<Stream> GetLicenseStreamAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        /// <summary>
        /// Remove a package's content from storage. This operation SHOULD succeed
        /// even if the package does not exist.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task DeleteAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        /// <summary>
        /// Persist a signed copy of the package to the 'signed/' subdirectory.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="signedPackageStream">The signed package's nupkg stream.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task SaveSignedPackageAsync(string id, NuGetVersion version, Stream signedPackageStream, CancellationToken cancellationToken);

        /// <summary>
        /// Check if a signed copy of the package exists in storage.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True if a signed copy exists, false otherwise.</returns>
        Task<bool> HasSignedPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve a signed package's nupkg stream if one exists.
        /// </summary>
        /// <param name="id">The package's id.</param>
        /// <param name="version">The package's version.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The signed package's nupkg stream, or null if no signed copy exists.</returns>
        Task<Stream> GetSignedPackageStreamOrNullAsync(string id, NuGetVersion version, CancellationToken cancellationToken);
    }
}

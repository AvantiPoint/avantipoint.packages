using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The Package Metadata client, used to fetch packages' metadata.
    /// 
    /// See https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource
    /// </summary>
    public interface IPackageMetadataService
    {
        /// <summary>
        /// Attempt to get a package's registration index, if it exists.
        /// See: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#registration-page
        /// </summary>
        /// <param name="packageId">The package's ID.</param>
        /// <param name="cancellationToken">A token to cancel the task.</param>
        /// <returns>The package's registration index, or null if the package does not exist</returns>
        Task<NuGetApiRegistrationIndexResponse> GetRegistrationIndexOrNullAsync(string packageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the metadata for a single package version, if the package exists.
        /// </summary>
        /// <param name="packageId">The package's id.</param>
        /// <param name="packageVersion">The package's version.</param>
        /// <param name="cancellationToken">A token to cancel the task.</param>
        /// <returns>The registration leaf, or null if the package does not exist.</returns>
        Task<RegistrationLeafResponse> GetRegistrationLeafOrNullAsync(
            string packageId,
            NuGetVersion packageVersion,
            CancellationToken cancellationToken = default);

        Task<PackageInfoCollection> GetPackageInfo(
            string packageId,
            string version = default,
            CancellationToken cancellationToken = default);
    }
}

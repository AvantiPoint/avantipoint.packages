#nullable enable
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Service for signing NuGet packages with repository signatures.
/// </summary>
public interface IPackageSigningService
{
    /// <summary>
    /// Signs a NuGet package with a repository signature.
    /// </summary>
    /// <param name="packageId">The package ID.</param>
    /// <param name="version">The package version.</param>
    /// <param name="packageStream">The unsigned package stream. Will be read from current position.</param>
    /// <param name="certificate">The certificate to sign with.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the signed package.</returns>
    Task<Stream> SignPackageAsync(
        string packageId,
        NuGetVersion version,
        Stream packageStream,
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a package already has a repository signature.
    /// </summary>
    /// <param name="packageStream">The package stream to check. Position will be reset after check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the package has a repository signature, false otherwise.</returns>
    Task<bool> IsPackageSignedAsync(
        Stream packageStream,
        CancellationToken cancellationToken = default);
}

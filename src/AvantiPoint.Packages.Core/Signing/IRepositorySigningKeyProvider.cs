#nullable enable
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Provides a certificate for signing repository packages.
/// </summary>
public interface IRepositorySigningKeyProvider
{
    /// <summary>
    /// Gets the signing certificate, or null if signing is disabled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The X.509 certificate to use for signing, or null if signing is disabled.</returns>
    Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default);
}

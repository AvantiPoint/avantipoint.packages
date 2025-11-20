#nullable enable
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// A signing key provider that returns null, indicating that signing is disabled.
/// </summary>
public class NullSigningKeyProvider : INullSigningKeyProvider
{
    /// <inheritdoc />
    public Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<X509Certificate2?>(null);
    }
}

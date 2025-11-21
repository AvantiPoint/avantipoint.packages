namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Specifies the hash algorithm used to compute a certificate fingerprint.
/// </summary>
public enum CertificateHashAlgorithm
{
    /// <summary>
    /// SHA-256 hash algorithm (256-bit output, 64 hex characters).
    /// </summary>
    Sha256 = 256,

    /// <summary>
    /// SHA-384 hash algorithm (384-bit output, 96 hex characters).
    /// </summary>
    Sha384 = 384,

    /// <summary>
    /// SHA-512 hash algorithm (512-bit output, 128 hex characters).
    /// </summary>
    Sha512 = 512
}


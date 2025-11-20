namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Specifies the mode for repository package signing.
/// </summary>
public enum SigningMode
{
    /// <summary>
    /// Generate and use a self-signed certificate for signing.
    /// </summary>
    SelfSigned,

    /// <summary>
    /// Use a certificate from the certificate store or a file.
    /// </summary>
    StoredCertificate
}

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
    StoredCertificate,

    /// <summary>
    /// Use a certificate stored in Azure Key Vault (Premium tier with HSM-backed keys).
    /// Requires <see cref="AvantiPoint.Packages.Signing.Azure"/> package.
    /// </summary>
    AzureKeyVault,

    /// <summary>
    /// Use AWS Key Management Service (KMS) with HSM-backed keys.
    /// Requires <see cref="AvantiPoint.Packages.Signing.Aws"/> package.
    /// </summary>
    AwsKms,

    /// <summary>
    /// Use AWS Signer managed code signing service.
    /// Requires <see cref="AvantiPoint.Packages.Signing.Aws"/> package.
    /// </summary>
    AwsSigner,

    /// <summary>
    /// Use Google Cloud Key Management Service (KMS) with HSM protection level.
    /// Requires <see cref="AvantiPoint.Packages.Signing.Gcp"/> package.
    /// </summary>
    GcpKms,

    /// <summary>
    /// Use Google Cloud HSM (fully managed HSM service).
    /// Requires <see cref="AvantiPoint.Packages.Signing.Gcp"/> package.
    /// </summary>
    GcpHsm
}

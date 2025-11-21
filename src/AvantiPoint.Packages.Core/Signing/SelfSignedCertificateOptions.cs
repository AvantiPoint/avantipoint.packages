using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Configuration options for self-signed certificate generation.
/// </summary>
public class SelfSignedCertificateOptions
{
    /// <summary>
    /// Complete subject name (e.g., "CN=MyServer, O=MyOrg, OU=Dev, C=US").
    /// If null or empty, will be constructed from Organization, OrganizationalUnit, Country, and the configured ServerName.
    /// </summary>
    public string? SubjectName { get; set; }

    /// <summary>
    /// Organization (O) component of the subject name.
    /// Used to construct SubjectName if SubjectName is not provided.
    /// </summary>
    public string? Organization { get; set; }

    /// <summary>
    /// Organizational Unit (OU) component of the subject name.
    /// Used to construct SubjectName if SubjectName is not provided.
    /// </summary>
    public string? OrganizationalUnit { get; set; }

    /// <summary>
    /// Country (C) component of the subject name (2-letter ISO code).
    /// Used to construct SubjectName if SubjectName is not provided.
    /// </summary>
    [StringLength(2, MinimumLength = 2, ErrorMessage = "Country must be a 2-letter ISO code.")]
    public string? Country { get; set; }

    /// <summary>
    /// Hash algorithm to use for signing (e.g., SHA256, SHA384, SHA512).
    /// </summary>
    public string HashAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// RSA key size in bits. Only industry-standard values are allowed: 2048, 3072, or 4096.
    /// </summary>
    public RsaKeySize KeySize { get; set; } = RsaKeySize.KeySize4096;

    /// <summary>
    /// Validity period of the certificate in days.
    /// </summary>
    [Range(1, 3650)]
    public int ValidityInDays { get; set; } = 3650;

    /// <summary>
    /// Path within the storage abstraction where the PFX will be persisted.
    /// </summary>
    public string CertificatePath { get; set; } = "certs/repository-signing.pfx";
}

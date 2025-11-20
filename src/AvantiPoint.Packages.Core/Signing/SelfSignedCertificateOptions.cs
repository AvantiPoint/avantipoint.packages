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
    /// Key size in bits. Must be between 2048 and 16384.
    /// </summary>
    [Range(2048, 16384)]
    public int KeySize { get; set; } = 4096;

    /// <summary>
    /// Validity period of the certificate in days.
    /// </summary>
    [Range(1, 3650)]
    public int ValidityInDays { get; set; } = 3650;

    /// <summary>
    /// Path within the storage abstraction where the PFX will be persisted.
    /// </summary>
    public string CertificatePath { get; set; } = "certs/repository-signing.pfx";

    /// <summary>
    /// Configuration key used to resolve the PFX password from configuration/secret store.
    /// If null, an empty password will be used.
    /// </summary>
    public string? CertificatePasswordSecret { get; set; }
}

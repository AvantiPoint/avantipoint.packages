using System;
using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core.Signing;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Represents a certificate that has been used to sign packages in this repository.
    /// This serves as the source of truth for valid repository signing certificates,
    /// even if the certificate file is no longer accessible.
    /// </summary>
    public class RepositorySigningCertificate
    {
        /// <summary>
        /// Primary key.
        /// </summary>
        public int Key { get; set; }

        /// <summary>
        /// Certificate fingerprint (lowercase hex string) computed using the hash algorithm specified in <see cref="HashAlgorithm"/>.
        /// This is the primary identifier for certificate verification.
        /// </summary>
        [Required]
        [MaxLength(128)] // Max length for SHA-512 (128 hex characters)
        public string Fingerprint { get; set; }

        /// <summary>
        /// The hash algorithm used to compute the fingerprint.
        /// </summary>
        public CertificateHashAlgorithm HashAlgorithm { get; set; } = CertificateHashAlgorithm.Sha256;

        /// <summary>
        /// The subject distinguished name of the certificate.
        /// Example: "CN=NuGet.org Repository by Microsoft, O=NuGet.org Repository by Microsoft, L=Redmond, S=Washington, C=US"
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        /// <summary>
        /// The issuer distinguished name of the certificate.
        /// Example: "CN=DigiCert SHA2 Assured ID Code Signing CA, OU=www.digicert.com, O=DigiCert Inc, C=US"
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Issuer { get; set; }

        /// <summary>
        /// The date and time when the certificate becomes valid (UTC).
        /// </summary>
        public DateTime NotBefore { get; set; }

        /// <summary>
        /// The date and time when the certificate expires (UTC).
        /// </summary>
        public DateTime NotAfter { get; set; }

        /// <summary>
        /// The date and time when this certificate was first used to sign a package in this repository (UTC).
        /// </summary>
        public DateTime FirstUsed { get; set; }

        /// <summary>
        /// The date and time when this certificate was last used to sign a package in this repository (UTC).
        /// Updated each time the certificate is used for signing.
        /// </summary>
        public DateTime LastUsed { get; set; }

        /// <summary>
        /// Indicates whether this certificate is currently active for signing operations.
        /// Set to false if the certificate should no longer be used (e.g., compromised, revoked).
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Optional URL where the certificate file (.crt) can be downloaded.
        /// May be null if the certificate is stored only in the database or is no longer accessible.
        /// </summary>
        [MaxLength(2000)]
        public string ContentUrl { get; set; }

        /// <summary>
        /// The public certificate in DER format (X.509 certificate bytes).
        /// This allows clients to download and verify the certificate even after certificate rotation.
        /// Stored as binary data in the database for long-term persistence.
        /// </summary>
        public byte[] PublicCertificateBytes { get; set; }

        /// <summary>
        /// Optional notes about this certificate (reason for revocation, etc).
        /// </summary>
        public string Notes { get; set; }
    }
}

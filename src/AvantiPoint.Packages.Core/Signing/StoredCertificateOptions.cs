#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Configuration options for loading a certificate from a store or file.
/// </summary>
public class StoredCertificateOptions : IValidatableObject
{
    /// <summary>
    /// Certificate thumbprint (SHA1 hash) to locate the certificate in the store.
    /// Required when loading from a certificate store.
    /// </summary>
    public string? Thumbprint { get; set; }

    /// <summary>
    /// Certificate store name (e.g., My, Root, TrustedPeople).
    /// </summary>
    public StoreName? StoreName { get; set; }

    /// <summary>
    /// Certificate store location (CurrentUser or LocalMachine).
    /// </summary>
    public StoreLocation? StoreLocation { get; set; }

    /// <summary>
    /// Path to a PFX/P12 certificate file.
    /// Required when loading from a file.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Password for the certificate file or private key.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Configuration key used to resolve the certificate password from configuration/secret store.
    /// If provided, this takes precedence over Password property.
    /// </summary>
    public string? CertificatePasswordSecret { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasStoreConfig = !string.IsNullOrWhiteSpace(Thumbprint);
        var hasFileConfig = !string.IsNullOrWhiteSpace(FilePath);

        if (!hasStoreConfig && !hasFileConfig)
        {
            yield return new ValidationResult(
                "Either Thumbprint (for certificate store) or FilePath (for file) must be specified.",
                new[] { nameof(Thumbprint), nameof(FilePath) });
        }

        if (hasStoreConfig && hasFileConfig)
        {
            yield return new ValidationResult(
                "Cannot specify both Thumbprint and FilePath. Choose one certificate source.",
                new[] { nameof(Thumbprint), nameof(FilePath) });
        }

        if (hasStoreConfig)
        {
            if (StoreName is null)
            {
                yield return new ValidationResult(
                    "StoreName is required when using Thumbprint.",
                    new[] { nameof(StoreName) });
            }

            if (StoreLocation is null)
            {
                yield return new ValidationResult(
                    "StoreLocation is required when using Thumbprint.",
                    new[] { nameof(StoreLocation) });
            }
        }
    }
}

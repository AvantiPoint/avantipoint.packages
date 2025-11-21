#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Signing.Gcp;

/// <summary>
/// Configuration options for Google Cloud Key Management Service (KMS) certificate signing.
/// </summary>
public class GcpKmsOptions : IValidatableObject
{
    /// <summary>
    /// The GCP project ID.
    /// </summary>
    [Required]
    public string? ProjectId { get; set; }

    /// <summary>
    /// The location of the key ring (e.g., us-east1, global).
    /// </summary>
    [Required]
    public string? Location { get; set; }

    /// <summary>
    /// The name of the key ring.
    /// </summary>
    [Required]
    public string? KeyRing { get; set; }

    /// <summary>
    /// The name of the crypto key.
    /// </summary>
    [Required]
    public string? KeyName { get; set; }

    /// <summary>
    /// The version of the key to use. If null, the primary version will be used.
    /// </summary>
    public string? KeyVersion { get; set; }

    /// <summary>
    /// Path to the service account JSON key file.
    /// If not provided, uses Application Default Credentials (ADC).
    /// </summary>
    public string? ServiceAccountKeyPath { get; set; }

    /// <summary>
    /// Configuration key to resolve the service account key path from configuration/secret store (env var, etc.).
    /// If provided, takes precedence over <see cref="ServiceAccountKeyPath"/>.
    /// </summary>
    public string? ServiceAccountKeyPathConfigurationKey { get; set; }

    /// <summary>
    /// The protection level for the key.
    /// Default: HSM (Hardware Security Module)
    /// </summary>
    public GcpProtectionLevel ProtectionLevel { get; set; } = GcpProtectionLevel.Hsm;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            yield return new ValidationResult(
                "GcpKms.ProjectId is required.",
                new[] { nameof(ProjectId) });
        }

        if (string.IsNullOrWhiteSpace(Location))
        {
            yield return new ValidationResult(
                "GcpKms.Location is required.",
                new[] { nameof(Location) });
        }

        if (string.IsNullOrWhiteSpace(KeyRing))
        {
            yield return new ValidationResult(
                "GcpKms.KeyRing is required.",
                new[] { nameof(KeyRing) });
        }

        if (string.IsNullOrWhiteSpace(KeyName))
        {
            yield return new ValidationResult(
                "GcpKms.KeyName is required.",
                new[] { nameof(KeyName) });
        }
    }
}

/// <summary>
/// Protection level for GCP KMS keys.
/// </summary>
public enum GcpProtectionLevel
{
    /// <summary>
    /// Software protection (keys stored in software).
    /// </summary>
    Software,

    /// <summary>
    /// Hardware Security Module protection (FIPS 140-2 Level 3 validated).
    /// </summary>
    Hsm
}


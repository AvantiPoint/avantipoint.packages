#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Signing.Gcp;

/// <summary>
/// Configuration options for Google Cloud HSM (fully managed HSM service).
/// </summary>
public class GcpHsmOptions : IValidatableObject
{
    /// <summary>
    /// The GCP project ID.
    /// </summary>
    [Required]
    public string? ProjectId { get; set; }

    /// <summary>
    /// The location of the HSM cluster (e.g., us-east1).
    /// </summary>
    [Required]
    public string? Location { get; set; }

    /// <summary>
    /// The name of the HSM cluster.
    /// </summary>
    [Required]
    public string? ClusterName { get; set; }

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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            yield return new ValidationResult(
                "GcpHsm.ProjectId is required.",
                new[] { nameof(ProjectId) });
        }

        if (string.IsNullOrWhiteSpace(Location))
        {
            yield return new ValidationResult(
                "GcpHsm.Location is required.",
                new[] { nameof(Location) });
        }

        if (string.IsNullOrWhiteSpace(ClusterName))
        {
            yield return new ValidationResult(
                "GcpHsm.ClusterName is required.",
                new[] { nameof(ClusterName) });
        }
    }
}


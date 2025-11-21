#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Signing.Aws;

/// <summary>
/// Configuration options for AWS Key Management Service (KMS) certificate signing.
/// </summary>
public class AwsKmsOptions : IValidatableObject
{
    /// <summary>
    /// The AWS region (e.g., us-east-1, us-west-2).
    /// </summary>
    [Required]
    public string? Region { get; set; }

    /// <summary>
    /// The KMS key ID or ARN for the signing key.
    /// </summary>
    [Required]
    public string? KeyId { get; set; }

    /// <summary>
    /// AWS access key ID. If not provided, uses default credential chain (IAM roles, environment variables, etc.).
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS secret access key. If not provided, uses default credential chain.
    /// Can be provided via configuration key specified in <see cref="SecretAccessKeyConfigurationKey"/>.
    /// </summary>
    public string? SecretAccessKey { get; set; }

    /// <summary>
    /// Configuration key to resolve the secret access key from configuration/secret store (env var, etc.).
    /// If provided, takes precedence over <see cref="SecretAccessKey"/>.
    /// </summary>
    public string? SecretAccessKeyConfigurationKey { get; set; }

    /// <summary>
    /// The signing algorithm to use (e.g., RSASSA_PSS_SHA_256, RSASSA_PSS_SHA_384, RSASSA_PSS_SHA_512).
    /// Default: RSASSA_PSS_SHA_256
    /// </summary>
    public string SigningAlgorithm { get; set; } = "RSASSA_PSS_SHA_256";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Region))
        {
            yield return new ValidationResult(
                "AwsKms.Region is required.",
                new[] { nameof(Region) });
        }

        if (string.IsNullOrWhiteSpace(KeyId))
        {
            yield return new ValidationResult(
                "AwsKms.KeyId is required.",
                new[] { nameof(KeyId) });
        }
    }
}


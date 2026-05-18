#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Signing.Aws;

/// <summary>
/// Configuration options for AWS Signer managed code signing service.
/// </summary>
public class AwsSignerOptions : IValidatableObject
{
    /// <summary>
    /// The AWS region (e.g., us-east-1, us-west-2).
    /// </summary>
    [Required]
    public string? Region { get; set; }

    /// <summary>
    /// The signing profile name in AWS Signer.
    /// </summary>
    [Required]
    public string? ProfileName { get; set; }

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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Region))
        {
            yield return new ValidationResult(
                "AwsSigner.Region is required.",
                new[] { nameof(Region) });
        }

        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            yield return new ValidationResult(
                "AwsSigner.ProfileName is required.",
                new[] { nameof(ProfileName) });
        }
    }
}


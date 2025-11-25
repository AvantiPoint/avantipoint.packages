#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Configuration options for repository package signing.
/// </summary>
public class SigningOptions : IValidatableObject
{
    /// <summary>
    /// The signing provider name. If null, signing is disabled.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Configuration key used to resolve the certificate password from configuration/secret store (env var, etc.).
    /// This password is used for both self-signed certificate generation and stored certificate loading.
    /// If null, an empty password will be used.
    /// </summary>
    public string? CertificatePasswordSecret { get; set; }

    /// <summary>
    /// The resolved certificate password. This is set automatically during configuration binding
    /// by resolving the value from <see cref="CertificatePasswordSecret"/>.
    /// </summary>
    internal string? CertificatePassword { get; set; }

        /// <summary>
        /// RFC 3161 timestamp server URL for signing packages.
        /// If null or empty, DigiCert's timestamp server (http://timestamp.digicert.com) will be used as the default.
        /// Set to empty string to disable timestamping (not recommended - signatures will become invalid when certificate expires).
        /// </summary>
        public string? TimestampServerUrl { get; set; }

        /// <summary>
        /// Publish-time policy for handling incoming repository signatures (API/CLI pushes).
        /// Default is <see cref="UpstreamSignatureBehavior.ReSign"/>.
        /// </summary>
        public UpstreamSignatureBehavior PublishSignaturePolicy { get; set; } = UpstreamSignatureBehavior.ReSign;

    /// <summary>
    /// Options for self-signed certificate generation.
    /// Required when Mode is SelfSigned.
    /// </summary>
    public SelfSignedCertificateOptions? SelfSigned { get; set; }

    /// <summary>
    /// Options for loading a certificate from a store or file.
    /// Required when Mode is StoredCertificate.
    /// </summary>
    public StoredCertificateOptions? StoredCertificate { get; set; }

    // Note: Cloud provider options (AzureKeyVault, AwsKms, AwsSigner, GcpKms, GcpHsm) are defined
    // in their respective packages and configured via extension methods (AddAzureKeyVaultSigning, etc.)
    // They are not included here to avoid circular dependencies.

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Provider))
        {
            yield break;
        }

        if (Provider.Equals(SigningProviderNames.SelfSigned, StringComparison.OrdinalIgnoreCase))
        {
            if (SelfSigned is null)
            {
                yield return new ValidationResult(
                    "Signing.SelfSigned must be configured when Signing.Provider is 'SelfSigned'.",
                    new[] { nameof(SelfSigned) });
            }
            else
            {
                foreach (var result in ValidateObject(SelfSigned))
                {
                    yield return result;
                }
            }

            yield break;
        }

        if (Provider.Equals(SigningProviderNames.StoredCertificate, StringComparison.OrdinalIgnoreCase))
        {
            if (StoredCertificate is null)
            {
                yield return new ValidationResult(
                    "Signing.StoredCertificate must be configured when Signing.Provider is 'StoredCertificate'.",
                    new[] { nameof(StoredCertificate) });
            }
            else
            {
                foreach (var result in ValidateObject(StoredCertificate))
                {
                    yield return result;
                }
            }
        }
    }

    private static IEnumerable<ValidationResult> ValidateObject(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            instance,
            new ValidationContext(instance),
            results,
            validateAllProperties: true);
        return results;
    }
}

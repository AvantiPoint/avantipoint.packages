using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Configuration options for repository package signing.
/// </summary>
public class SigningOptions : IValidatableObject
{
    /// <summary>
    /// The signing mode. If null, signing is disabled.
    /// </summary>
    public SigningMode? Mode { get; set; }

    /// <summary>
    /// Options for self-signed certificate generation.
    /// Required when Mode is SelfSigned.
    /// </summary>
    public SelfSignedCertificateOptions SelfSigned { get; set; }

    /// <summary>
    /// Options for loading a certificate from a store or file.
    /// Required when Mode is StoredCertificate.
    /// </summary>
    public StoredCertificateOptions StoredCertificate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Mode is null)
        {
            yield break;
        }

        switch (Mode)
        {
            case SigningMode.SelfSigned:
                if (SelfSigned is null)
                {
                    yield return new ValidationResult(
                        "Signing.SelfSigned must be configured when Signing.Mode is 'SelfSigned'.",
                        new[] { nameof(SelfSigned) });
                }
                else
                {
                    foreach (var result in ValidateObject(SelfSigned))
                    {
                        yield return result;
                    }
                }

                break;

            case SigningMode.StoredCertificate:
                if (StoredCertificate is null)
                {
                    yield return new ValidationResult(
                        "Signing.StoredCertificate must be configured when Signing.Mode is 'StoredCertificate'.",
                        new[] { nameof(StoredCertificate) });
                }
                else
                {
                    foreach (var result in ValidateObject(StoredCertificate))
                    {
                        yield return result;
                    }
                }

                break;
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

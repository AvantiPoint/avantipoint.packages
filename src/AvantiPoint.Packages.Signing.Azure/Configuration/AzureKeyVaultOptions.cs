#nullable enable
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Signing.Azure;

/// <summary>
/// Configuration options for Azure Key Vault certificate signing.
/// </summary>
public class AzureKeyVaultOptions : IValidatableObject
{
    /// <summary>
    /// The Azure Key Vault URI (e.g., https://myvault.vault.azure.net/).
    /// </summary>
    [Required]
    public string? VaultUri { get; set; }

    /// <summary>
    /// The name of the certificate in Azure Key Vault.
    /// </summary>
    [Required]
    public string? CertificateName { get; set; }

    /// <summary>
    /// The version of the certificate to use. If null, the latest version will be used.
    /// </summary>
    public string? CertificateVersion { get; set; }

    /// <summary>
    /// Authentication mode for Azure Key Vault.
    /// Default: Uses DefaultAzureCredential (supports Managed Identity, Azure CLI, Visual Studio, etc.)
    /// </summary>
    public AzureKeyVaultAuthenticationMode AuthenticationMode { get; set; } = AzureKeyVaultAuthenticationMode.Default;

    /// <summary>
    /// Tenant ID for ClientSecret authentication mode.
    /// Required when <see cref="AuthenticationMode"/> is <see cref="AzureKeyVaultAuthenticationMode.ClientSecret"/>.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Client ID for ClientSecret authentication mode.
    /// Required when <see cref="AuthenticationMode"/> is <see cref="AzureKeyVaultAuthenticationMode.ClientSecret"/>.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for ClientSecret authentication mode.
    /// Required when <see cref="AuthenticationMode"/> is <see cref="AzureKeyVaultAuthenticationMode.ClientSecret"/>.
    /// Can be provided via configuration key specified in <see cref="ClientSecretConfigurationKey"/>.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Configuration key to resolve the client secret from configuration/secret store (env var, etc.).
    /// If provided, takes precedence over <see cref="ClientSecret"/>.
    /// </summary>
    public string? ClientSecretConfigurationKey { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(VaultUri))
        {
            yield return new ValidationResult(
                "AzureKeyVault.VaultUri is required.",
                new[] { nameof(VaultUri) });
        }

        if (string.IsNullOrWhiteSpace(CertificateName))
        {
            yield return new ValidationResult(
                "AzureKeyVault.CertificateName is required.",
                new[] { nameof(CertificateName) });
        }

        if (AuthenticationMode == AzureKeyVaultAuthenticationMode.ClientSecret)
        {
            if (string.IsNullOrWhiteSpace(TenantId))
            {
                yield return new ValidationResult(
                    "AzureKeyVault.TenantId is required when AuthenticationMode is ClientSecret.",
                    new[] { nameof(TenantId) });
            }

            if (string.IsNullOrWhiteSpace(ClientId))
            {
                yield return new ValidationResult(
                    "AzureKeyVault.ClientId is required when AuthenticationMode is ClientSecret.",
                    new[] { nameof(ClientId) });
            }

            if (string.IsNullOrWhiteSpace(ClientSecret) && string.IsNullOrWhiteSpace(ClientSecretConfigurationKey))
            {
                yield return new ValidationResult(
                    "AzureKeyVault.ClientSecret or AzureKeyVault.ClientSecretConfigurationKey is required when AuthenticationMode is ClientSecret.",
                    new[] { nameof(ClientSecret), nameof(ClientSecretConfigurationKey) });
            }
        }
    }
}

/// <summary>
/// Authentication modes for Azure Key Vault.
/// </summary>
public enum AzureKeyVaultAuthenticationMode
{
    /// <summary>
    /// Uses DefaultAzureCredential, which supports:
    /// - Managed Identity (when running on Azure)
    /// - Azure CLI (az login)
    /// - Visual Studio
    /// - Environment variables
    /// </summary>
    Default,

    /// <summary>
    /// Uses Managed Identity authentication (requires running on Azure with managed identity enabled).
    /// </summary>
    ManagedIdentity,

    /// <summary>
    /// Uses client secret authentication (requires TenantId, ClientId, and ClientSecret).
    /// </summary>
    ClientSecret
}


#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Signing.Azure;

/// <summary>
/// Repository signing key provider that retrieves certificates from Azure Key Vault.
/// </summary>
public class AzureKeyVaultRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<AzureKeyVaultRepositorySigningKeyProvider> _logger;
    private readonly AzureKeyVaultOptions _options;
    private readonly IConfiguration _configuration;
    private readonly CertificateClient _certificateClient;
    private X509Certificate2? _cachedCertificate;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    public AzureKeyVaultRepositorySigningKeyProvider(
        ILogger<AzureKeyVaultRepositorySigningKeyProvider> logger,
        IOptions<AzureKeyVaultOptions> options,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        var vaultUri = new Uri(_options.VaultUri!);
        var credential = CreateCredential();
        _certificateClient = new CertificateClient(vaultUri, credential);

        _logger.LogInformation(
            "Initialized Azure Key Vault signing provider for vault {VaultUri} with certificate {CertificateName}",
            _options.VaultUri,
            _options.CertificateName);
    }

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache (refresh every 5 minutes to avoid excessive Key Vault calls)
            if (_cachedCertificate != null && DateTimeOffset.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached certificate from Azure Key Vault");
                return _cachedCertificate;
            }

            // Download the full certificate (including private key) if exportable
            // DownloadCertificateAsync returns X509Certificate2 directly (PFX format)
            X509Certificate2 x509Certificate;
            if (!string.IsNullOrWhiteSpace(_options.CertificateVersion))
            {
                _logger.LogDebug(
                    "Downloading certificate {CertificateName} version {Version} with private key from Azure Key Vault",
                    _options.CertificateName,
                    _options.CertificateVersion);
                var response = await _certificateClient.DownloadCertificateAsync(
                    _options.CertificateName!,
                    version: _options.CertificateVersion,
                    cancellationToken: cancellationToken);
                x509Certificate = response.Value;
            }
            else
            {
                _logger.LogDebug(
                    "Downloading latest certificate {CertificateName} with private key from Azure Key Vault",
                    _options.CertificateName);
                var response = await _certificateClient.DownloadCertificateAsync(
                    _options.CertificateName!,
                    cancellationToken: cancellationToken);
                x509Certificate = response.Value;
            }

            // Note: For HSM-backed certificates, the private key may not be exportable.
            // In that case, DownloadCertificateAsync will fail or return a certificate without private key.
            // For true HSM-backed keys, we would need to use Key Vault's signing operations directly.
            
            // Verify private key is accessible
            if (!x509Certificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    $"Certificate {_options.CertificateName} from Azure Key Vault does not have an accessible private key. " +
                    "The certificate may be HSM-backed and non-exportable. " +
                    "For HSM-backed certificates, the certificate must be marked as exportable, " +
                    "or use a different signing mode that supports HSM-backed keys.");
            }

            // Verify certificate has a private key (Key Vault manages the private key, but we need to verify access)
            // Note: With Key Vault, the private key is stored in the HSM and signing operations are performed
            // by Key Vault. However, for NuGet signing, we need the full certificate including private key.
            // This requires the certificate to be exportable or we need to use Key Vault signing operations directly.
            // For now, we'll retrieve the certificate and assume the private key is accessible via Key Vault.

            _logger.LogInformation(
                "Successfully retrieved certificate {CertificateName} (thumbprint: {Thumbprint}) from Azure Key Vault",
                _options.CertificateName,
                x509Certificate.Thumbprint);

            // Cache for 5 minutes
            _cachedCertificate = x509Certificate;
            _cacheExpiry = DateTimeOffset.UtcNow.AddMinutes(5);

            return x509Certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve certificate {CertificateName} from Azure Key Vault {VaultUri}",
                _options.CertificateName,
                _options.VaultUri);
            throw;
        }
    }

    private TokenCredential CreateCredential()
    {
        return _options.AuthenticationMode switch
        {
            AzureKeyVaultAuthenticationMode.Default => new DefaultAzureCredential(),
            AzureKeyVaultAuthenticationMode.ManagedIdentity => new ManagedIdentityCredential(),
            AzureKeyVaultAuthenticationMode.ClientSecret => CreateClientSecretCredential(),
            _ => throw new NotSupportedException($"Authentication mode '{_options.AuthenticationMode}' is not supported.")
        };
    }

    private TokenCredential CreateClientSecretCredential()
    {
        var tenantId = _options.TenantId ?? throw new InvalidOperationException("TenantId is required for ClientSecret authentication.");
        var clientId = _options.ClientId ?? throw new InvalidOperationException("ClientId is required for ClientSecret authentication.");

        // Resolve client secret from configuration if specified
        string? clientSecret = null;
        if (!string.IsNullOrWhiteSpace(_options.ClientSecretConfigurationKey))
        {
            clientSecret = _configuration[_options.ClientSecretConfigurationKey];
        }

        clientSecret ??= _options.ClientSecret;

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("ClientSecret or ClientSecretConfigurationKey must be provided for ClientSecret authentication.");
        }

        return new ClientSecretCredential(tenantId, clientId, clientSecret);
    }
}


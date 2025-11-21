#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Signing.Gcp;

/// <summary>
/// Repository signing key provider that uses Google Cloud KMS for signing operations.
/// Note: GCP KMS does not export private keys. Signing operations must be performed using KMS APIs.
/// This implementation retrieves the public key for verification purposes.
/// </summary>
public class GcpKmsRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<GcpKmsRepositorySigningKeyProvider> _logger;
    private readonly GcpKmsOptions _options;
    private readonly IConfiguration _configuration;
    private readonly KeyManagementServiceClient _kmsClient;
    private X509Certificate2? _cachedCertificate;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    public GcpKmsRepositorySigningKeyProvider(
        ILogger<GcpKmsRepositorySigningKeyProvider> logger,
        IOptions<GcpKmsOptions> options,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Build the key name
        var keyName = BuildKeyName();

        // Create KMS client
        // Note: For service account authentication, set GOOGLE_APPLICATION_CREDENTIALS environment variable
        // or use the ServiceAccountKeyPath configuration
        var clientBuilder = new KeyManagementServiceClientBuilder();
        
        // Resolve service account key path from configuration if specified
        string? serviceAccountKeyPath = null;
        if (!string.IsNullOrWhiteSpace(_options.ServiceAccountKeyPathConfigurationKey))
        {
            serviceAccountKeyPath = _configuration[_options.ServiceAccountKeyPathConfigurationKey];
        }

        serviceAccountKeyPath ??= _options.ServiceAccountKeyPath;

        if (!string.IsNullOrWhiteSpace(serviceAccountKeyPath))
        {
            // Set credentials from service account key file
            Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", serviceAccountKeyPath);
        }

        _kmsClient = clientBuilder.Build();

        _logger.LogInformation(
            "Initialized GCP KMS signing provider for project {ProjectId} with key {KeyName}",
            _options.ProjectId,
            keyName);
    }

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache (refresh every 5 minutes)
            if (_cachedCertificate != null && DateTimeOffset.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached certificate from GCP KMS");
                return _cachedCertificate;
            }

            // Build the key name
            var keyName = BuildKeyName();

            // Get the public key from KMS
            _logger.LogDebug(
                "Retrieving public key for KMS key {KeyName}",
                keyName);

            var cryptoKeyVersionName = string.IsNullOrWhiteSpace(_options.KeyVersion)
                ? $"{keyName}/cryptoKeyVersions/1" // Primary version
                : $"{keyName}/cryptoKeyVersions/{_options.KeyVersion}";

            var getPublicKeyRequest = new GetPublicKeyRequest
            {
                Name = cryptoKeyVersionName
            };

            var publicKey = await _kmsClient.GetPublicKeyAsync(getPublicKeyRequest, cancellationToken);

            // GCP KMS returns the public key in PEM format
            // Note: GCP KMS does not provide a full X.509 certificate, only the public key.
            // For signing, we would need to use KMS signing operations directly.
            // This is a limitation - we cannot use standard X509Certificate2 signing with KMS keys.
            // We would need a custom signing implementation that uses KMS AsymmetricSign API.

            _logger.LogWarning(
                "GCP KMS does not provide X.509 certificates directly. " +
                "The public key has been retrieved, but signing operations require using KMS AsymmetricSign API directly. " +
                "This provider currently returns null - a custom signing implementation is required.");

            // TODO: Implement custom signing using KMS AsymmetricSign API
            // For now, return null to indicate this mode requires additional implementation
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve key from GCP KMS for project {ProjectId}",
                _options.ProjectId);
            throw;
        }
    }

    private string BuildKeyName()
    {
        var keyVersion = string.IsNullOrWhiteSpace(_options.KeyVersion) ? "1" : _options.KeyVersion;
        return $"projects/{_options.ProjectId}/locations/{_options.Location}/keyRings/{_options.KeyRing}/cryptoKeys/{_options.KeyName}";
    }
}


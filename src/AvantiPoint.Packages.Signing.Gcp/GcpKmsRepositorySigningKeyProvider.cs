#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Google.Cloud.Kms.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Signing.Gcp;

/// <summary>
/// Repository signing key provider that uses Google Cloud KMS for signing operations.
/// Note: GCP KMS does not export private keys. Signing operations must be performed using KMS APIs.
/// This implementation retrieves and caches public key material for verification purposes.
/// </summary>
public class GcpKmsRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<GcpKmsRepositorySigningKeyProvider> _logger;
    private readonly GcpKmsOptions _options;
    private readonly IConfiguration _configuration;
    private readonly KeyManagementServiceClient _kmsClient;
    private readonly RepositorySigningCertificateCache _certificateCache = new();

    public GcpKmsRepositorySigningKeyProvider(
        ILogger<GcpKmsRepositorySigningKeyProvider> logger,
        IOptions<GcpKmsOptions> options,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        var keyName = BuildKeyName();

        var clientBuilder = new KeyManagementServiceClientBuilder();

        string? serviceAccountKeyPath = null;
        if (!string.IsNullOrWhiteSpace(_options.ServiceAccountKeyPathConfigurationKey))
        {
            serviceAccountKeyPath = _configuration[_options.ServiceAccountKeyPathConfigurationKey];
        }

        serviceAccountKeyPath ??= _options.ServiceAccountKeyPath;

        if (!string.IsNullOrWhiteSpace(serviceAccountKeyPath))
        {
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
        if (_certificateCache.TryGet(out var cachedCertificate))
        {
            _logger.LogDebug("Returning cached certificate from GCP KMS");
            return cachedCertificate;
        }

        try
        {
            var keyName = BuildKeyName();

            _logger.LogDebug(
                "Retrieving public key for KMS key {KeyName}",
                keyName);

            var cryptoKeyVersionName = string.IsNullOrWhiteSpace(_options.KeyVersion)
                ? $"{keyName}/cryptoKeyVersions/1"
                : $"{keyName}/cryptoKeyVersions/{_options.KeyVersion}";

            var getPublicKeyRequest = new GetPublicKeyRequest
            {
                Name = cryptoKeyVersionName
            };

            var publicKey = await _kmsClient.GetPublicKeyAsync(getPublicKeyRequest, cancellationToken);
            var certificate = SigningCertificateParser.TryCreateFromPem(publicKey.Pem);

            if (certificate is null)
            {
                _logger.LogWarning(
                    "GCP KMS does not provide an exportable X.509 certificate for key {KeyName}. " +
                    "Public key material was retrieved and cached, but signing requires using the KMS AsymmetricSign API directly.",
                    keyName);
            }
            else
            {
                _logger.LogInformation(
                    "Retrieved certificate (thumbprint: {Thumbprint}) from GCP KMS key {KeyName}",
                    certificate.Thumbprint,
                    keyName);
            }

            _certificateCache.Set(certificate);
            return certificate;
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
        return $"projects/{_options.ProjectId}/locations/{_options.Location}/keyRings/{_options.KeyRing}/cryptoKeys/{_options.KeyName}";
    }
}

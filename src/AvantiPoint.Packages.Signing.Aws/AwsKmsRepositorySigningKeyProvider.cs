#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Signing.Aws;

/// <summary>
/// Repository signing key provider that uses AWS KMS for signing operations.
/// Note: AWS KMS does not export private keys. Signing operations must be performed using KMS APIs.
/// This implementation retrieves the public certificate for verification purposes.
/// </summary>
public class AwsKmsRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<AwsKmsRepositorySigningKeyProvider> _logger;
    private readonly AwsKmsOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IAmazonKeyManagementService _kmsClient;
    private X509Certificate2? _cachedCertificate;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    public AwsKmsRepositorySigningKeyProvider(
        ILogger<AwsKmsRepositorySigningKeyProvider> logger,
        IOptions<AwsKmsOptions> options,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        var region = Amazon.RegionEndpoint.GetBySystemName(_options.Region!);
        var credentials = CreateCredentials();
        _kmsClient = new AmazonKeyManagementServiceClient(credentials, region);

        _logger.LogInformation(
            "Initialized AWS KMS signing provider for region {Region} with key {KeyId}",
            _options.Region,
            _options.KeyId);
    }

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache (refresh every 5 minutes)
            if (_cachedCertificate != null && DateTimeOffset.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached certificate from AWS KMS");
                return _cachedCertificate;
            }

            // Get the public key from KMS
            _logger.LogDebug(
                "Retrieving public key for KMS key {KeyId}",
                _options.KeyId);

            var getPublicKeyRequest = new GetPublicKeyRequest
            {
                KeyId = _options.KeyId
            };

            var response = await _kmsClient.GetPublicKeyAsync(getPublicKeyRequest, cancellationToken);

            // Convert AWS KMS public key to X509Certificate2
            // Note: AWS KMS does not provide a full X.509 certificate, only the public key.
            // For signing, we would need to use KMS signing operations directly.
            // This is a limitation - we cannot use standard X509Certificate2 signing with KMS keys.
            // We would need a custom signing implementation that uses KMS Sign API.

            _logger.LogWarning(
                "AWS KMS does not provide X.509 certificates directly. " +
                "The public key has been retrieved, but signing operations require using KMS Sign API directly. " +
                "This provider currently returns null - a custom signing implementation is required.");

            // TODO: Implement custom signing using KMS Sign API
            // For now, return null to indicate this mode requires additional implementation
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve key {KeyId} from AWS KMS in region {Region}",
                _options.KeyId,
                _options.Region);
            throw;
        }
    }

    private AWSCredentials CreateCredentials()
    {
        // Resolve secret access key from configuration if specified
        string? secretAccessKey = null;
        if (!string.IsNullOrWhiteSpace(_options.SecretAccessKeyConfigurationKey))
        {
            secretAccessKey = _configuration[_options.SecretAccessKeyConfigurationKey];
        }

        secretAccessKey ??= _options.SecretAccessKey;

        if (!string.IsNullOrWhiteSpace(_options.AccessKeyId) && !string.IsNullOrWhiteSpace(secretAccessKey))
        {
            return new Amazon.Runtime.BasicAWSCredentials(_options.AccessKeyId, secretAccessKey);
        }

        // Use default credential chain (IAM roles, environment variables, etc.)
        return new Amazon.Runtime.EnvironmentVariablesAWSCredentials();
    }
}


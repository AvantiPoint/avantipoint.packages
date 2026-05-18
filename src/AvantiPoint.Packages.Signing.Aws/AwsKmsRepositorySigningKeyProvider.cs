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
/// This implementation retrieves and caches public key material for verification purposes.
/// </summary>
public class AwsKmsRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<AwsKmsRepositorySigningKeyProvider> _logger;
    private readonly AwsKmsOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IAmazonKeyManagementService _kmsClient;
    private readonly RepositorySigningCertificateCache _certificateCache = new();

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
        if (_certificateCache.TryGet(out var cachedCertificate))
        {
            _logger.LogDebug("Returning cached certificate from AWS KMS");
            return cachedCertificate;
        }

        try
        {
            _logger.LogDebug(
                "Retrieving public key for KMS key {KeyId}",
                _options.KeyId);

            var getPublicKeyRequest = new GetPublicKeyRequest
            {
                KeyId = _options.KeyId
            };

            var response = await _kmsClient.GetPublicKeyAsync(getPublicKeyRequest, cancellationToken);
            var publicKeyBytes = response.PublicKey.ToArray();
            var certificate = SigningCertificateParser.TryCreateFromDer(publicKeyBytes);

            if (certificate is null)
            {
                _logger.LogWarning(
                    "AWS KMS does not provide an exportable X.509 certificate for key {KeyId}. " +
                    "Public key material was retrieved and cached, but signing requires using the KMS Sign API directly.",
                    _options.KeyId);
            }
            else
            {
                _logger.LogInformation(
                    "Retrieved certificate (thumbprint: {Thumbprint}) from AWS KMS key {KeyId}",
                    certificate.Thumbprint,
                    _options.KeyId);
            }

            _certificateCache.Set(certificate);
            return certificate;
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
            return new BasicAWSCredentials(_options.AccessKeyId, secretAccessKey);
        }

        // Use default credential chain (IAM roles, environment variables, etc.)
        return new EnvironmentVariablesAWSCredentials();
    }
}

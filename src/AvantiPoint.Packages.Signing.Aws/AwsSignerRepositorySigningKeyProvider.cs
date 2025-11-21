#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Signer;
using Amazon.Signer.Model;
using Amazon.Runtime;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Signing.Aws;

/// <summary>
/// Repository signing key provider that uses AWS Signer managed code signing service.
/// Note: AWS Signer is designed for code signing workflows and manages certificates automatically.
/// </summary>
public class AwsSignerRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<AwsSignerRepositorySigningKeyProvider> _logger;
    private readonly AwsSignerOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IAmazonSigner _signerClient;
    private X509Certificate2? _cachedCertificate;
    private DateTimeOffset _cacheExpiry = DateTimeOffset.MinValue;

    public AwsSignerRepositorySigningKeyProvider(
        ILogger<AwsSignerRepositorySigningKeyProvider> logger,
        IOptions<AwsSignerOptions> options,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        var region = Amazon.RegionEndpoint.GetBySystemName(_options.Region!);
        var credentials = CreateCredentials();
        _signerClient = new AmazonSignerClient(credentials, region);

        _logger.LogInformation(
            "Initialized AWS Signer signing provider for region {Region} with profile {ProfileName}",
            _options.Region,
            _options.ProfileName);
    }

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check cache (refresh every 5 minutes)
            if (_cachedCertificate != null && DateTimeOffset.UtcNow < _cacheExpiry)
            {
                _logger.LogDebug("Returning cached certificate from AWS Signer");
                return _cachedCertificate;
            }

            // Get signing profile to retrieve certificate
            _logger.LogDebug(
                "Retrieving signing profile {ProfileName} from AWS Signer",
                _options.ProfileName);

            var getProfileRequest = new GetSigningProfileRequest
            {
                ProfileName = _options.ProfileName
            };

            var profileResponse = await _signerClient.GetSigningProfileAsync(getProfileRequest, cancellationToken);

            // AWS Signer manages certificates internally and does not provide direct access to X.509 certificates.
            // Signing operations must be performed using Signer's StartSigningJob API.
            // This is a limitation - we cannot use standard X509Certificate2 signing with Signer.
            // We would need a custom signing implementation that uses Signer's signing job API.

            _logger.LogWarning(
                "AWS Signer does not provide direct access to X.509 certificates. " +
                "Signing operations require using Signer's StartSigningJob API. " +
                "This provider currently returns null - a custom signing implementation is required.");

            // TODO: Implement custom signing using Signer's StartSigningJob API
            // For now, return null to indicate this mode requires additional implementation
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve signing profile {ProfileName} from AWS Signer in region {Region}",
                _options.ProfileName,
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


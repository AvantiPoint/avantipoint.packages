#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.Signer;
using Amazon.Signer.Model;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Signing.Aws;

/// <summary>
/// Repository signing key provider that uses AWS Signer managed code signing service.
/// Note: AWS Signer is designed for code signing workflows and manages certificates internally.
/// </summary>
public class AwsSignerRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly ILogger<AwsSignerRepositorySigningKeyProvider> _logger;
    private readonly AwsSignerOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IAmazonSigner _signerClient;
    private readonly RepositorySigningCertificateCache _certificateCache = new();

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
        if (_certificateCache.TryGet(out var cachedCertificate))
        {
            _logger.LogDebug("Returning cached signing profile result from AWS Signer");
            return cachedCertificate;
        }

        try
        {
            _logger.LogDebug(
                "Retrieving signing profile {ProfileName} from AWS Signer",
                _options.ProfileName);

            var getProfileRequest = new GetSigningProfileRequest
            {
                ProfileName = _options.ProfileName
            };

            await _signerClient.GetSigningProfileAsync(getProfileRequest, cancellationToken);

            _logger.LogWarning(
                "AWS Signer does not provide direct access to X.509 certificates for profile {ProfileName}. " +
                "The profile lookup was cached, but signing requires using Signer's StartSigningJob API.",
                _options.ProfileName);

            _certificateCache.Set(null);
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

        return new EnvironmentVariablesAWSCredentials();
    }
}

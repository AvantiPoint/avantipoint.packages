using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Core.Signing
{
    /// <summary>
    /// Validates repository signing configuration during application startup.
    /// Fails fast if signing is enabled but the certificate cannot be loaded.
    /// </summary>
    public class SigningStartupValidationService : IHostedService
    {
        private readonly IRepositorySigningKeyProvider _signingKeyProvider;
        private readonly ILogger<SigningStartupValidationService> _logger;

        public SigningStartupValidationService(
            IRepositorySigningKeyProvider signingKeyProvider,
            ILogger<SigningStartupValidationService> logger)
        {
            _signingKeyProvider = signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Check if repository signing is enabled
            if (_signingKeyProvider is INullSigningKeyProvider)
            {
                _logger.LogInformation("Repository signing is disabled");
                return;
            }

            _logger.LogInformation("Repository signing is enabled, validating certificate...");

            try
            {
                var certificate = await _signingKeyProvider.GetSigningCertificateAsync(cancellationToken);

                if (certificate == null)
                {
                    var errorMessage = "Repository signing is enabled but no certificate could be loaded. " +
                        "Please check your signing configuration.";
                    _logger.LogCritical(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                // Validate the certificate has a private key
                if (!certificate.HasPrivateKey)
                {
                    var errorMessage = $"Repository signing certificate (Subject: {certificate.Subject}) does not have a private key. " +
                        "A private key is required for signing packages.";
                    _logger.LogCritical(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                // Validate the certificate is not expired
                var now = DateTimeOffset.UtcNow;
                if (certificate.NotAfter < now.DateTime)
                {
                    var errorMessage = $"Repository signing certificate (Subject: {certificate.Subject}) expired on {certificate.NotAfter:yyyy-MM-dd}. " +
                        "Please update your signing configuration with a valid certificate.";
                    _logger.LogCritical(errorMessage);
                    throw new InvalidOperationException(errorMessage);
                }

                // Warn if the certificate expires soon (within 30 days)
                var expiresIn = certificate.NotAfter - now.DateTime;
                if (expiresIn.TotalDays < 30)
                {
                    _logger.LogWarning(
                        "Repository signing certificate (Subject: {Subject}) expires in {Days} days on {ExpiryDate:yyyy-MM-dd}. " +
                        "Please renew the certificate soon.",
                        certificate.Subject,
                        (int)expiresIn.TotalDays,
                        certificate.NotAfter);
                }

                _logger.LogInformation(
                    "Repository signing certificate validated successfully. Subject: {Subject}, Expires: {ExpiryDate:yyyy-MM-dd}",
                    certificate.Subject,
                    certificate.NotAfter);
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var errorMessage = "Failed to validate repository signing certificate during startup. " +
                    "Please check your signing configuration.";
                _logger.LogCritical(ex, errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // No cleanup needed
            return Task.CompletedTask;
        }
    }
}

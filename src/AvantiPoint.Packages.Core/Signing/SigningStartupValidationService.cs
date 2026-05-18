#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Validates repository signing configuration during application startup.
/// Fails fast if signing is enabled but the certificate cannot be loaded.
/// </summary>
public class SigningStartupValidationService(
    IServiceProvider services,
    ILogger<SigningStartupValidationService> logger,
    TimeProvider timeProvider,
    CertificateValidationHelper validationHelper) : IHostedService
{

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var signingKeyProvider = scope.ServiceProvider.GetRequiredService<IRepositorySigningKeyProvider>();
        // Check if repository signing is enabled
        if (signingKeyProvider is INullSigningKeyProvider)
        {
            logger.LogInformation("Repository signing is disabled");
            return;
        }

        logger.LogInformation("Repository signing is enabled, validating certificate...");

        try
        {
            var certificate = await signingKeyProvider.GetSigningCertificateAsync(cancellationToken);

            if (certificate == null)
            {
                var errorMessage = "Repository signing is enabled but no certificate could be loaded. " +
                    "Please check your signing configuration.";
                logger.LogCritical(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Validate the certificate has a private key
            if (!certificate.HasPrivateKey)
            {
                var errorMessage = $"Repository signing certificate (Subject: {certificate.Subject}) does not have a private key. " +
                    "A private key is required for signing packages.";
                logger.LogCritical(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Validate the certificate is not expired and has at least 5 minutes remaining
            // This ensures the certificate can be used for signing without expiring mid-operation
            if (validationHelper.IsCertificateExpired(certificate))
            {
                var timeUntilExpiry = validationHelper.GetTimeUntilExpiry(certificate);
                var errorMessage = timeUntilExpiry.TotalSeconds <= 0
                    ? $"Repository signing certificate (Subject: {certificate.Subject}) expired on {certificate.NotAfter:yyyy-MM-dd HH:mm:ss} UTC. " +
                      "Please update your signing configuration with a valid certificate."
                    : $"Repository signing certificate (Subject: {certificate.Subject}) expires in {timeUntilExpiry.TotalMinutes:F1} minutes (at {certificate.NotAfter:yyyy-MM-dd HH:mm:ss} UTC), " +
                      $"which is less than the required {CertificateValidationHelper.MinimumValidityPeriod.TotalMinutes} minute buffer. " +
                      "Please update your signing configuration with a certificate that has sufficient remaining validity.";
                logger.LogCritical(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Warn if the certificate expires soon (within 30 days)
            var now = timeProvider.GetUtcNow();
            var expiresIn = certificate.NotAfter.ToUniversalTime() - now.UtcDateTime;
            if (expiresIn.TotalDays < 30)
            {
                logger.LogWarning(
                    "Repository signing certificate (Subject: {Subject}) expires in {Days} days on {ExpiryDate:yyyy-MM-dd}. " +
                    "Please renew the certificate soon.",
                    certificate.Subject,
                    (int)expiresIn.TotalDays,
                    certificate.NotAfter);
            }

            logger.LogInformation(
                "Repository signing certificate validated successfully. Subject: {Subject}, Expires: {ExpiryDate:yyyy-MM-dd}",
                certificate.Subject,
                certificate.NotAfter);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            var errorMessage = "Failed to validate repository signing certificate during startup. " +
                "Please check your signing configuration.";
            logger.LogCritical(ex, errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // No cleanup needed
        return Task.CompletedTask;
    }
}

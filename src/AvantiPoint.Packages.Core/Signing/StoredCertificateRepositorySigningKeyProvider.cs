#nullable enable
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Loads a certificate from the certificate store or a file for package signing.
/// </summary>
public class StoredCertificateRepositorySigningKeyProvider(
    IOptions<SigningOptions> signingOptions,
    ILogger<StoredCertificateRepositorySigningKeyProvider> logger,
    RepositorySigningCertificateService certificateService,
    IStorageService storage,
    CertificateValidationHelper validationHelper) : IRepositorySigningKeyProvider
{
    private readonly SigningOptions _signingOptions = signingOptions.Value;
    private readonly StoredCertificateOptions _options = signingOptions.Value.StoredCertificate
        ?? throw new InvalidOperationException("StoredCertificateOptions are not configured.");
    private X509Certificate2? _cachedCertificate;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        // Check cached certificate expiration on every call
        if (_cachedCertificate is not null)
        {
            if (IsCertificateExpired(_cachedCertificate))
            {
                logger.LogWarning(
                    "Cached certificate has expired. Thumbprint: {Thumbprint}, Expired: {NotAfter}",
                    _cachedCertificate.Thumbprint,
                    _cachedCertificate.NotAfter);
                _cachedCertificate = null; // Clear cache to force reload
            }
            else
            {
                return _cachedCertificate;
            }
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cachedCertificate is not null && !IsCertificateExpired(_cachedCertificate))
            {
                return _cachedCertificate;
            }

            X509Certificate2? certificate = null;

            if (!string.IsNullOrWhiteSpace(_options.Thumbprint))
            {
                certificate = LoadFromStore();
            }
            else if (!string.IsNullOrWhiteSpace(_options.FilePath))
            {
                certificate = await LoadFromFileAsync(cancellationToken);
            }

            if (certificate is null)
            {
                throw new InvalidOperationException("Failed to load certificate from store or file.");
            }

            // Validate certificate is not expired - fail fast for stored certificates
            // Certificate must be valid for at least 5 minutes to prevent expiration during signing
            if (validationHelper.IsCertificateExpired(certificate))
            {
                var timeUntilExpiry = validationHelper.GetTimeUntilExpiry(certificate);
                var now = System.TimeProvider.System.GetUtcNow().UtcDateTime;
                
                string errorMessage;
                if (certificate.NotBefore.ToUniversalTime() > now)
                {
                    // Certificate is not yet valid
                    errorMessage = $"Stored certificate (Subject: {certificate.Subject}, Thumbprint: {certificate.Thumbprint}) is not yet valid. " +
                        $"It becomes valid on {certificate.NotBefore:yyyy-MM-dd HH:mm:ss} UTC. " +
                        "Please update your signing configuration with a valid certificate.";
                }
                else if (timeUntilExpiry.TotalSeconds <= 0)
                {
                    // Certificate has expired
                    errorMessage = $"Stored certificate (Subject: {certificate.Subject}, Thumbprint: {certificate.Thumbprint}) expired on {certificate.NotAfter:yyyy-MM-dd HH:mm:ss} UTC. " +
                        "Please update your signing configuration with a valid certificate.";
                }
                else
                {
                    // Certificate expires soon
                    errorMessage = $"Stored certificate (Subject: {certificate.Subject}, Thumbprint: {certificate.Thumbprint}) expires in {timeUntilExpiry.TotalMinutes:F1} minutes (at {certificate.NotAfter:yyyy-MM-dd HH:mm:ss} UTC), " +
                        $"which is less than the required {CertificateValidationHelper.MinimumValidityPeriod.TotalMinutes} minute buffer. " +
                        "Please update your signing configuration with a certificate that has sufficient remaining validity.";
                }
                logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Record certificate usage in database
            await certificateService.RecordCertificateUsageAsync(certificate, cancellationToken);

            _cachedCertificate = certificate;

            logger.LogInformation(
                "Certificate loaded successfully. Thumbprint: {Thumbprint}, Subject: {Subject}, Valid until: {NotAfter}",
                certificate.Thumbprint,
                certificate.Subject,
                certificate.NotAfter);

            return _cachedCertificate;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsCertificateExpired(X509Certificate2 certificate)
    {
        return validationHelper.IsCertificateExpired(certificate);
    }

    private X509Certificate2? LoadFromStore()
    {
        if (_options.StoreName is null || _options.StoreLocation is null)
        {
            throw new InvalidOperationException("StoreName and StoreLocation must be specified when loading from store.");
        }

        logger.LogInformation(
            "Loading certificate from store. StoreName: {StoreName}, StoreLocation: {StoreLocation}, Thumbprint: {Thumbprint}",
            _options.StoreName,
            _options.StoreLocation,
            _options.Thumbprint);

        using var store = new X509Store(_options.StoreName.Value, _options.StoreLocation.Value);
        store.Open(OpenFlags.ReadOnly);

        var certificates = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            _options.Thumbprint!,
            validOnly: false);

        if (certificates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Certificate with thumbprint '{_options.Thumbprint}' not found in store '{_options.StoreName}' at location '{_options.StoreLocation}'.");
        }

        var certificate = certificates[0];

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"Certificate with thumbprint '{_options.Thumbprint}' does not have a private key.");
        }

        return certificate;
    }

    private async Task<X509Certificate2> LoadFromFileAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.FilePath))
        {
            throw new InvalidOperationException("FilePath must be specified when loading from file.");
        }

        logger.LogInformation("Loading certificate from storage: {FilePath}", _options.FilePath);

        Stream? stream;
        try
        {
            stream = await storage.GetAsync(_options.FilePath, cancellationToken);
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException($"Certificate file not found in storage: {_options.FilePath}");
        }
        
        if (stream is null)
        {
            throw new FileNotFoundException($"Certificate file not found in storage: {_options.FilePath}");
        }

        byte[] pfxBytes;
        await using (stream)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            pfxBytes = ms.ToArray();
        }

        // Resolve password: CertificatePasswordSecret (from top-level) -> Password property -> empty
        var password = _signingOptions.CertificatePassword;
        if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(_options.Password))
        {
            password = _options.Password;
        }

        var certificate = string.IsNullOrWhiteSpace(password)
            ? X509CertificateLoader.LoadPkcs12(pfxBytes, null)
            : X509CertificateLoader.LoadPkcs12(pfxBytes, password);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"Certificate loaded from '{_options.FilePath}' does not have a private key.");
        }

        return certificate;
    }
}

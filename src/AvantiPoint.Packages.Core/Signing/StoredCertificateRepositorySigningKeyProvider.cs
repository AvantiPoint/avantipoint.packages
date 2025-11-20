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
public class StoredCertificateRepositorySigningKeyProvider : IRepositorySigningKeyProvider
{
    private readonly StoredCertificateOptions _options;
    private readonly ILogger<StoredCertificateRepositorySigningKeyProvider> _logger;
    private readonly RepositorySigningCertificateService _certificateService;
    private readonly IStorageService _storage;
    private X509Certificate2? _cachedCertificate;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public StoredCertificateRepositorySigningKeyProvider(
        IOptions<SigningOptions> signingOptions,
        ILogger<StoredCertificateRepositorySigningKeyProvider> logger,
        RepositorySigningCertificateService certificateService,
        IStorageService storage)
    {
        _options = signingOptions.Value.StoredCertificate
            ?? throw new InvalidOperationException("StoredCertificateOptions are not configured.");
        _logger = logger;
        _certificateService = certificateService;
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <inheritdoc />
    public async Task<X509Certificate2?> GetSigningCertificateAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedCertificate is not null)
        {
            return _cachedCertificate;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedCertificate is not null)
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

            // Record certificate usage in database
            await _certificateService.RecordCertificateUsageAsync(certificate, cancellationToken);

            _cachedCertificate = certificate;

            _logger.LogInformation(
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

    private X509Certificate2? LoadFromStore()
    {
        if (_options.StoreName is null || _options.StoreLocation is null)
        {
            throw new InvalidOperationException("StoreName and StoreLocation must be specified when loading from store.");
        }

        _logger.LogInformation(
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

        _logger.LogInformation("Loading certificate from storage: {FilePath}", _options.FilePath);

        using var stream = await _storage.GetAsync(_options.FilePath, cancellationToken);
        if (stream is null)
        {
            throw new FileNotFoundException($"Certificate file not found in storage: {_options.FilePath}");
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var pfxBytes = ms.ToArray();

        var certificate = string.IsNullOrWhiteSpace(_options.Password)
            ? X509CertificateLoader.LoadPkcs12(pfxBytes, null)
            : X509CertificateLoader.LoadPkcs12(pfxBytes, _options.Password);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"Certificate loaded from '{_options.FilePath}' does not have a private key.");
        }

        return certificate;
    }
}

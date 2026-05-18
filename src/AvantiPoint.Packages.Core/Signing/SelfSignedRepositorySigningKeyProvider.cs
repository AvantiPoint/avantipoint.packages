#nullable enable
using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Generates or reuses a self-signed certificate for package signing.
/// Persists the certificate using IStorageService for reuse across restarts.
/// </summary>
public class SelfSignedRepositorySigningKeyProvider(
    IOptions<SigningOptions> signingOptions,
    IOptions<PackageFeedOptions> feedOptions,
    IStorageService storage,
    RepositorySigningCertificateService certificateService,
    ILogger<SelfSignedRepositorySigningKeyProvider> logger,
    TimeProvider timeProvider,
    CertificateValidationHelper validationHelper) : IRepositorySigningKeyProvider
{
    private readonly SelfSignedCertificateOptions _options = signingOptions.Value.SelfSigned
        ?? throw new InvalidOperationException("SelfSignedCertificateOptions are not configured.");
    private readonly string _subjectName = BuildSubjectName(
        signingOptions.Value.SelfSigned ?? throw new InvalidOperationException("SelfSignedCertificateOptions are not configured."),
        feedOptions.Value.Shield?.ServerName);
    private readonly string _certificatePassword = signingOptions.Value.CertificatePassword ?? string.Empty;
    private X509Certificate2? _cachedCertificate;
    private readonly SemaphoreSlim _lock = new(1, 1);


    private static string BuildSubjectName(SelfSignedCertificateOptions options, string? serverName)
    {
        // If SubjectName is explicitly provided, use it
        if (!string.IsNullOrWhiteSpace(options.SubjectName))
        {
            return options.SubjectName;
        }

        // Otherwise, construct from helper properties
        var parts = new System.Collections.Generic.List<string>();

        // CN always comes from ServerName
        var cn = !string.IsNullOrWhiteSpace(serverName) ? serverName : "AvantiPoint Packages";
        parts.Add($"CN={cn}");

        if (!string.IsNullOrWhiteSpace(options.Organization))
        {
            parts.Add($"O={options.Organization}");
        }

        if (!string.IsNullOrWhiteSpace(options.OrganizationalUnit))
        {
            parts.Add($"OU={options.OrganizationalUnit}");
        }

        if (!string.IsNullOrWhiteSpace(options.Country))
        {
            parts.Add($"C={options.Country}");
        }

        return string.Join(", ", parts);
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

            // Try to load existing certificate from storage
            var existingCert = await TryLoadExistingCertificateAsync(cancellationToken);
            if (existingCert is not null && IsCertificateValid(existingCert))
            {
                logger.LogInformation(
                    "Loaded existing self-signed certificate. Thumbprint: {Thumbprint}, Valid until: {NotAfter}",
                    existingCert.Thumbprint,
                    existingCert.NotAfter);

                // Record certificate usage in database
                await certificateService.RecordCertificateUsageAsync(existingCert, cancellationToken);

                _cachedCertificate = existingCert;
                return _cachedCertificate;
            }

            if (existingCert is not null)
            {
                logger.LogInformation(
                    "Existing certificate is invalid or configuration has changed. Generating new certificate.");
                existingCert.Dispose();
            }

            // Generate new certificate
            var newCert = GenerateSelfSignedCertificate();

            // Persist to storage
            await SaveCertificateAsync(newCert, cancellationToken);

            // Record certificate usage in database
            await certificateService.RecordCertificateUsageAsync(newCert, cancellationToken);

            _cachedCertificate = newCert;

            logger.LogInformation(
                "Self-signed certificate generated and saved. Thumbprint: {Thumbprint}, Valid until: {NotAfter}",
                newCert.Thumbprint,
                newCert.NotAfter);

            return _cachedCertificate;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<X509Certificate2?> TryLoadExistingCertificateAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var stream = await storage.GetAsync(_options.CertificatePath, cancellationToken);
            if (stream is null)
            {
                logger.LogDebug("No existing certificate found in storage at {Path}.", _options.CertificatePath);
                return null;
            }

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            var pfxBytes = ms.ToArray();

            var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, _certificatePassword, X509KeyStorageFlags.Exportable);
            return cert;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load existing certificate from storage.");
            return null;
        }
    }

    private bool IsCertificateValid(X509Certificate2 certificate)
    {
        // Check if certificate has expired or will expire within the minimum validity period (5 minutes)
        // This ensures the certificate can be used for signing without expiring mid-operation
        if (validationHelper.IsCertificateExpired(certificate))
        {
            var timeUntilExpiry = validationHelper.GetTimeUntilExpiry(certificate);
            logger.LogInformation(
                "Certificate is expired or will expire within {Minutes} minutes (NotAfter: {NotAfter}).",
                CertificateValidationHelper.MinimumValidityPeriod.TotalMinutes,
                certificate.NotAfter);
            return false;
        }

        // Check if certificate will expire soon (within 7 days) - this triggers rotation
        var now = timeProvider.GetUtcNow().DateTime;
        if (certificate.NotAfter <= now.AddDays(7))
        {
            logger.LogInformation("Certificate is expiring soon (within 7 days) (NotAfter: {NotAfter}).", certificate.NotAfter);
            return false;
        }

        // Validate that certificate properties match configuration
        if (certificate.SubjectName.Name != _subjectName)
        {
            logger.LogInformation(
                "Certificate subject name does not match configuration. Expected: {Expected}, Actual: {Actual}",
                _subjectName,
                certificate.SubjectName.Name);
            return false;
        }

        // Check key size (RSA only)
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null)
        {
            var expectedKeySize = (int)_options.KeySize;
            if (rsa.KeySize != expectedKeySize)
            {
                logger.LogInformation(
                    "Certificate key size does not match configuration. Expected: {Expected}, Actual: {Actual}",
                    expectedKeySize,
                    rsa.KeySize);
                return false;
            }
        }

        // Validate hash algorithm by checking signature algorithm OID
        var signatureAlgorithm = certificate.SignatureAlgorithm.FriendlyName ?? string.Empty;
        var expectedHashName = _options.HashAlgorithm.ToUpperInvariant();
        if (!signatureAlgorithm.Contains(expectedHashName, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Certificate hash algorithm does not match configuration. Expected: {Expected}, Actual: {Actual}",
                expectedHashName,
                signatureAlgorithm);
            return false;
        }

        return true;
    }

    private X509Certificate2 GenerateSelfSignedCertificate()
    {
        logger.LogInformation("Generating self-signed certificate with subject: {SubjectName}", _subjectName);

        var hashAlgorithm = ParseHashAlgorithm(_options.HashAlgorithm);
        using var rsa = RSA.Create((int)_options.KeySize);

        var request = new CertificateRequest(
            _subjectName,
            rsa,
            hashAlgorithm,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                critical: true));

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        var notBefore = timeProvider.GetUtcNow();
        var notAfter = notBefore.AddDays(_options.ValidityInDays);

        var certificate = request.CreateSelfSigned(notBefore, notAfter);

        return certificate;
    }

    private async Task SaveCertificateAsync(X509Certificate2 certificate, CancellationToken cancellationToken)
    {
        try
        {
            var pfxBytes = certificate.Export(X509ContentType.Pfx, _certificatePassword);
            using var ms = new MemoryStream(pfxBytes);
            await storage.PutAsync(_options.CertificatePath, ms, "application/x-pkcs12", cancellationToken);

            logger.LogDebug("Certificate saved to storage at {Path}.", _options.CertificatePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save certificate to storage.");
            throw;
        }
    }

    private static HashAlgorithmName ParseHashAlgorithm(string algorithmName)
    {
        return algorithmName.ToUpperInvariant() switch
        {
            "SHA256" => HashAlgorithmName.SHA256,
            "SHA384" => HashAlgorithmName.SHA384,
            "SHA512" => HashAlgorithmName.SHA512,
            _ => throw new NotSupportedException($"Hash algorithm '{algorithmName}' is not supported.")
        };
    }
}

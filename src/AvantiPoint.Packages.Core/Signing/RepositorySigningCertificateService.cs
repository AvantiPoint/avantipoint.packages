using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Service for tracking and managing repository signing certificates in the database.
/// </summary>
public class RepositorySigningCertificateService
{
    private readonly IContext _context;
    private readonly ILogger<RepositorySigningCertificateService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IUrlGenerator _urlGenerator;

    public RepositorySigningCertificateService(
        IContext context,
        ILogger<RepositorySigningCertificateService> logger,
        TimeProvider timeProvider,
        IUrlGenerator urlGenerator)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _urlGenerator = urlGenerator ?? throw new ArgumentNullException(nameof(urlGenerator));
    }

    /// <summary>
    /// Records or updates certificate usage in the database.
    /// This ensures we have a permanent record of certificates used for signing,
    /// even if the certificate file is later deleted.
    /// </summary>
    /// <param name="certificate">The certificate being used for signing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task RecordCertificateUsageAsync(
        X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        var fingerprint = ComputeFingerprint(certificate, CertificateHashAlgorithm.Sha256);

        var existing = await _context.RepositorySigningCertificates
            .FirstOrDefaultAsync(c => c.Fingerprint == fingerprint && c.HashAlgorithm == CertificateHashAlgorithm.Sha256, cancellationToken);

        // Extract public certificate bytes (DER format)
        var publicCertificateBytes = certificate.RawData;

        if (existing is not null)
        {
            // Update last used timestamp
            existing.LastUsed = _timeProvider.GetUtcNow().DateTime;

            // Update public certificate bytes and ContentUrl if not already set
            // This handles cases where existing records were created before this feature
            if (existing.PublicCertificateBytes is null || existing.PublicCertificateBytes.Length == 0)
            {
                existing.PublicCertificateBytes = publicCertificateBytes;
                existing.ContentUrl = _urlGenerator.GetCertificateDownloadUrl(fingerprint);
                _logger.LogDebug(
                    "Updated certificate record with public certificate bytes and ContentUrl for {Fingerprint}",
                    fingerprint);
            }

            _logger.LogDebug(
                "Updated last used timestamp for certificate {Fingerprint}",
                fingerprint);
        }
        else
        {
            // Create new certificate record
            var now = _timeProvider.GetUtcNow().DateTime;
            var cert = new RepositorySigningCertificate
            {
                Fingerprint = fingerprint,
                HashAlgorithm = CertificateHashAlgorithm.Sha256,
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                NotBefore = certificate.NotBefore.ToUniversalTime(),
                NotAfter = certificate.NotAfter.ToUniversalTime(),
                FirstUsed = now,
                LastUsed = now,
                IsActive = true,
                PublicCertificateBytes = publicCertificateBytes,
                ContentUrl = _urlGenerator.GetCertificateDownloadUrl(fingerprint)
            };

            _context.RepositorySigningCertificates.Add(cert);

            _logger.LogInformation(
                "Recorded new certificate usage. Subject: {Subject}, Fingerprint: {Fingerprint}, Valid until: {NotAfter}, ContentUrl: {ContentUrl}",
                cert.Subject,
                fingerprint,
                cert.NotAfter,
                cert.ContentUrl);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gets all active certificates that are valid for the RepositorySignatures endpoint.
    /// This includes certificates that may no longer be accessible as files but were
    /// historically used to sign packages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of active repository signing certificates.</returns>
    public async Task<System.Collections.Generic.List<RepositorySigningCertificate>> GetActiveCertificatesAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow().DateTime;

        return await _context.RepositorySigningCertificates
            .AsNoTracking()
            .Where(c => c.IsActive)
            // Include expired certificates if they were used within the last 90 days
            // This helps with package verification for recently published packages
            .Where(c => c.NotAfter >= now || c.LastUsed >= now.AddDays(-90))
            .OrderByDescending(c => c.FirstUsed)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Marks a certificate as inactive (e.g., if compromised or revoked).
    /// The certificate will no longer appear in the RepositorySignatures endpoint.
    /// </summary>
    /// <param name="fingerprint">Fingerprint of the certificate to deactivate.</param>
    /// <param name="hashAlgorithm">Hash algorithm used to compute the fingerprint.</param>
    /// <param name="notes">Optional notes about why the certificate was deactivated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeactivateCertificateAsync(
        string fingerprint,
        CertificateHashAlgorithm hashAlgorithm = CertificateHashAlgorithm.Sha256,
        string notes = null,
        CancellationToken cancellationToken = default)
    {
        var certificate = await _context.RepositorySigningCertificates
            .FirstOrDefaultAsync(c => c.Fingerprint == fingerprint && c.HashAlgorithm == hashAlgorithm, cancellationToken);

        if (certificate is null)
        {
            _logger.LogWarning("Attempted to deactivate non-existent certificate {Fingerprint} ({HashAlgorithm})", fingerprint, hashAlgorithm);
            return;
        }

        certificate.IsActive = false;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            certificate.Notes = notes;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Deactivated certificate {Fingerprint} ({HashAlgorithm}). Subject: {Subject}. Notes: {Notes}",
            fingerprint,
            hashAlgorithm,
            certificate.Subject,
            notes);
    }

    /// <summary>
    /// Computes a certificate fingerprint using the specified hash algorithm.
    /// </summary>
    /// <param name="certificate">The certificate to compute the fingerprint for.</param>
    /// <param name="hashAlgorithm">The hash algorithm to use.</param>
    /// <returns>The fingerprint as a lowercase hex string.</returns>
    public static string ComputeFingerprint(X509Certificate2 certificate, CertificateHashAlgorithm hashAlgorithm)
    {
        return hashAlgorithm switch
        {
            CertificateHashAlgorithm.Sha256 => ComputeSha256Fingerprint(certificate),
            CertificateHashAlgorithm.Sha384 => ComputeSha384Fingerprint(certificate),
            CertificateHashAlgorithm.Sha512 => ComputeSha512Fingerprint(certificate),
            _ => throw new ArgumentOutOfRangeException(nameof(hashAlgorithm), hashAlgorithm, "Unsupported hash algorithm")
        };
    }

    private static string ComputeSha256Fingerprint(X509Certificate2 certificate)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(certificate.RawData);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeSha384Fingerprint(X509Certificate2 certificate)
    {
        using var sha384 = SHA384.Create();
        var hash = sha384.ComputeHash(certificate.RawData);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeSha512Fingerprint(X509Certificate2 certificate)
    {
        using var sha512 = SHA512.Create();
        var hash = sha512.ComputeHash(certificate.RawData);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

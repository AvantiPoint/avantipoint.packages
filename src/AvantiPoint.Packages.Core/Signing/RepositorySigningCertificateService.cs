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

    public RepositorySigningCertificateService(
        IContext context,
        ILogger<RepositorySigningCertificateService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        var sha256 = ComputeSha256Fingerprint(certificate);

        var existing = await _context.RepositorySigningCertificates
            .FirstOrDefaultAsync(c => c.Sha256Fingerprint == sha256, cancellationToken);

        if (existing is not null)
        {
            // Update last used timestamp
            existing.LastUsed = DateTime.UtcNow;

            _logger.LogDebug(
                "Updated last used timestamp for certificate {Sha256}",
                sha256);
        }
        else
        {
            // Create new certificate record
            var now = DateTime.UtcNow;
            var cert = new RepositorySigningCertificate
            {
                Sha256Fingerprint = sha256,
                Sha384Fingerprint = ComputeSha384Fingerprint(certificate),
                Sha512Fingerprint = ComputeSha512Fingerprint(certificate),
                Subject = certificate.Subject,
                Issuer = certificate.Issuer,
                NotBefore = certificate.NotBefore.ToUniversalTime(),
                NotAfter = certificate.NotAfter.ToUniversalTime(),
                FirstUsed = now,
                LastUsed = now,
                IsActive = true
            };

            _context.RepositorySigningCertificates.Add(cert);

            _logger.LogInformation(
                "Recorded new certificate usage. Subject: {Subject}, SHA-256: {Sha256}, Valid until: {NotAfter}",
                cert.Subject,
                sha256,
                cert.NotAfter);
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
        var now = DateTime.UtcNow;

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
    /// <param name="sha256Fingerprint">SHA-256 fingerprint of the certificate to deactivate.</param>
    /// <param name="notes">Optional notes about why the certificate was deactivated.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DeactivateCertificateAsync(
        string sha256Fingerprint,
        string notes = null,
        CancellationToken cancellationToken = default)
    {
        var certificate = await _context.RepositorySigningCertificates
            .FirstOrDefaultAsync(c => c.Sha256Fingerprint == sha256Fingerprint, cancellationToken);

        if (certificate is null)
        {
            _logger.LogWarning("Attempted to deactivate non-existent certificate {Sha256}", sha256Fingerprint);
            return;
        }

        certificate.IsActive = false;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            certificate.Notes = notes;
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Deactivated certificate {Sha256}. Subject: {Subject}. Notes: {Notes}",
            sha256Fingerprint,
            certificate.Subject,
            notes);
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

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Hosting.Caching;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Hosting;

#nullable enable
internal static class CertificateDownload
{
    public static WebApplication MapCertificateDownloadApi(this WebApplication app)
    {
        app.MapGet("v3/certificates/{fingerprint}.crt", GetCertificate)
           .AllowAnonymous()
           .UseNugetCaching()
           .WithTags(nameof(CertificateDownload))
           .WithName(Routes.CertificateDownloadRouteName);

        return app;
    }

    [ProducesResponseType(typeof(byte[]), 200, "application/x-x509-ca-cert")]
    [ProducesResponseType(404)]
    private static async Task<IResult> GetCertificate(
        string fingerprint,
        IContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return Results.BadRequest("Fingerprint is required.");
        }

        // Normalize fingerprint to lowercase (fingerprints are stored as lowercase hex)
        fingerprint = fingerprint.ToLowerInvariant();

        // Find certificate by fingerprint (SHA-256 is the primary algorithm we use)
        var certificate = await context.RepositorySigningCertificates
            .AsNoTracking()
            .Where(c => c.Fingerprint == fingerprint && c.HashAlgorithm == CertificateHashAlgorithm.Sha256)
            .FirstOrDefaultAsync(cancellationToken);

        if (certificate is null || certificate.PublicCertificateBytes is null || certificate.PublicCertificateBytes.Length == 0)
        {
            return Results.NotFound($"Certificate with fingerprint {fingerprint} not found.");
        }

        // Return the certificate in DER format (application/x-x509-ca-cert)
        // This is the standard MIME type for X.509 certificates
        return Results.File(
            certificate.PublicCertificateBytes,
            contentType: "application/x-x509-ca-cert",
            fileDownloadName: $"certificate-{fingerprint}.crt",
            lastModified: certificate.NotAfter,
            enableRangeProcessing: false);
    }
}


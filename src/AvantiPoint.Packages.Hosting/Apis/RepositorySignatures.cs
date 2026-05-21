using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Hosting;

#nullable enable
internal static class RepositorySignatures
{
    public static WebApplication MapRepositorySignaturesApi(this WebApplication app)
    {
        app.MapGet("v3/repository-signatures/index.json", GetRepositorySignatures)
           .AllowAnonymous()
           .WithTags(nameof(RepositorySignatures))
           .WithName(Routes.RepositorySignaturesRouteName);

        return app;
    }

    [ProducesResponseType(typeof(RepositorySignaturesResponse), 200, "application/json")]
    private static async Task<IResult> GetRepositorySignatures(
        RepositorySigningCertificateService certificateService,
        CancellationToken cancellationToken)
    {
        var certificates = await certificateService.GetActiveCertificatesAsync(cancellationToken);

        var response = new RepositorySignaturesResponse
        {
            AllRepositorySigned = certificates.Any(),
            Certificates = certificates.Select(cert => new RepositoryCertificateInfo
            {
                Fingerprints = new CertificateFingerprints
                {
                    // Only return the stored fingerprint (typically SHA-256)
                    // Other fingerprints can be computed on-demand if needed
                    Sha256 = cert.HashAlgorithm == CertificateHashAlgorithm.Sha256 ? cert.Fingerprint : null,
                    Sha384 = cert.HashAlgorithm == CertificateHashAlgorithm.Sha384 ? cert.Fingerprint : null,
                    Sha512 = cert.HashAlgorithm == CertificateHashAlgorithm.Sha512 ? cert.Fingerprint : null
                },
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                NotBefore = cert.NotBefore,
                NotAfter = cert.NotAfter,
                ContentUrl = !string.IsNullOrWhiteSpace(cert.ContentUrl) ? cert.ContentUrl : null
            }).ToList()
        };

        return Results.Ok(response);
    }
}

/// <summary>
/// Represents the repository signatures response per NuGet protocol specification.
/// https://docs.microsoft.com/en-us/nuget/api/repository-signatures-resource
/// </summary>
/// <summary>
/// Represents certificate information in the repository signatures response.
/// </summary>
/// <summary>
/// Represents the fingerprints of a certificate.
/// </summary>

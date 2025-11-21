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
public class RepositorySignaturesResponse
{
    /// <summary>
    /// Indicates whether all packages in the repository are repository signed.
    /// </summary>
    [JsonPropertyName("allRepositorySigned")]
    public bool AllRepositorySigned { get; set; }

    /// <summary>
    /// The list of certificates used to repository sign packages.
    /// </summary>
    [JsonPropertyName("signingCertificates")]
    public List<RepositoryCertificateInfo> Certificates { get; set; } = new();
}

/// <summary>
/// Represents certificate information in the repository signatures response.
/// </summary>
public class RepositoryCertificateInfo
{
    /// <summary>
    /// The fingerprints of the certificate.
    /// </summary>
    [JsonPropertyName("fingerprints")]
    public CertificateFingerprints Fingerprints { get; set; } = new();

    /// <summary>
    /// The subject distinguished name.
    /// </summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// The issuer distinguished name.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the certificate becomes valid (UTC).
    /// </summary>
    [JsonPropertyName("notBefore")]
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// The date and time when the certificate expires (UTC).
    /// </summary>
    [JsonPropertyName("notAfter")]
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Optional URL where the certificate (.crt) can be downloaded.
    /// </summary>
    [JsonPropertyName("contentUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContentUrl { get; set; }
}

/// <summary>
/// Represents the fingerprints of a certificate.
/// </summary>
public class CertificateFingerprints
{
    /// <summary>
    /// The SHA-256 fingerprint (lowercase hex string).
    /// </summary>
    [JsonPropertyName("2.16.840.1.101.3.4.2.1")]
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// The SHA-384 fingerprint (lowercase hex string).
    /// </summary>
    [JsonPropertyName("2.16.840.1.101.3.4.2.2")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha384 { get; set; }

    /// <summary>
    /// The SHA-512 fingerprint (lowercase hex string).
    /// </summary>
    [JsonPropertyName("2.16.840.1.101.3.4.2.3")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha512 { get; set; }
}

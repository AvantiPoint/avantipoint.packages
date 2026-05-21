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

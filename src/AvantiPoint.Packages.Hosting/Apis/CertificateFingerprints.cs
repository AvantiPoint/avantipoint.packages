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

public class CertificateFingerprints
{
    /// <summary>
    /// The SHA-256 fingerprint (lowercase hex string).
    /// </summary>
    [JsonPropertyName("2.16.840.1.101.3.4.2.1")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Sha256 { get; set; }

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

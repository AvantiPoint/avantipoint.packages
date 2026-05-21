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

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Configuration options for the NuGet search service.
/// </summary>
public class NuGetSearchServiceOptions
{
    /// <summary>
    /// The NuGet v3 service index URL. If not specified, defaults to "/v3/index.json" (relative to current host).
    /// For external feeds, provide the full URL (e.g., "https://api.nuget.org/v3/index.json").
    /// </summary>
    public string ServiceIndexUrl { get; set; } = "/v3/index.json";

    /// <summary>
    /// Optional callback to configure the HttpClient for authenticated feeds.
    /// Receives the current HttpContext and the HttpClient to configure.
    /// Use this to add authentication headers based on the current user.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ConfigureHttpClient = (httpContext, httpClient) =>
    /// {
    ///     var username = httpContext.User.Identity?.Name;
    ///     var token = httpContext.Items["NuGetToken"] as string;
    ///     if (!string.IsNullOrEmpty(username) &amp;&amp; !string.IsNullOrEmpty(token))
    ///     {
    ///         var credentials = Convert.ToBase64String(
    ///             System.Text.Encoding.ASCII.GetBytes($"{username}:{token}"));
    ///         httpClient.DefaultRequestHeaders.Authorization = 
    ///             new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
    ///     }
    /// };
    /// </code>
    /// </example>
    public Action<HttpContext, HttpClient>? ConfigureHttpClient { get; set; }
}

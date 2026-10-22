using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace AvantiPoint.Packages.Host.Admin.Configuration;

/// <summary>
/// Defines which reverse proxies may supply public scheme, host, and client-address headers.
/// </summary>
public sealed class HostReverseProxyOptions
{
    public const string SectionName = "Host:ReverseProxy";

    /// <summary>
    /// Maximum number of trusted proxy hops to consume. Defaults to the immediate proxy only.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// Exact IP addresses of trusted reverse proxies.
    /// </summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>
    /// CIDR ranges containing trusted reverse proxies.
    /// </summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Optional allowlist for values accepted from X-Forwarded-Host.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    public void Apply(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (ForwardLimit < 1)
        {
            throw new InvalidOperationException($"{SectionName}:ForwardLimit must be at least 1.");
        }

        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedFor
            | ForwardedHeaders.XForwardedHost
            | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = ForwardLimit;

        foreach (var configuredProxy in KnownProxies)
        {
            if (!IPAddress.TryParse(configuredProxy?.Trim(), out var proxy))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownProxies contains invalid IP address '{configuredProxy}'.");
            }

            if (!options.KnownProxies.Contains(proxy))
            {
                options.KnownProxies.Add(proxy);
            }
        }

        foreach (var configuredNetwork in KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(configuredNetwork?.Trim(), out var network))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownNetworks contains invalid CIDR range '{configuredNetwork}'.");
            }

            if (network.PrefixLength == 0)
            {
                throw new InvalidOperationException(
                    $"{SectionName}:KnownNetworks cannot trust the entire address space ('{configuredNetwork}').");
            }

            if (!options.KnownIPNetworks.Contains(network))
            {
                options.KnownIPNetworks.Add(network);
            }
        }

        if (AllowedHosts.Length == 0)
        {
            return;
        }

        options.AllowedHosts.Clear();
        foreach (var allowedHost in AllowedHosts)
        {
            var host = allowedHost?.Trim();
            if (string.IsNullOrEmpty(host))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:AllowedHosts cannot contain an empty value.");
            }

            options.AllowedHosts.Add(host);
        }
    }
}

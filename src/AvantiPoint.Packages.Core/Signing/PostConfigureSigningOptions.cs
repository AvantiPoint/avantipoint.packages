using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core.Signing;

/// <summary>
/// Post-configures SigningOptions by resolving the certificate password from the configuration/secret store.
/// </summary>
internal class PostConfigureSigningOptions : IPostConfigureOptions<SigningOptions>
{
    private readonly IConfiguration _configuration;

    public PostConfigureSigningOptions(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public void PostConfigure(string? name, SigningOptions options)
    {
        if (options is null)
        {
            return;
        }

        // Resolve certificate password from configuration/secret store
        // Priority: CertificatePasswordSecret (from config) -> empty string
        if (!string.IsNullOrWhiteSpace(options.CertificatePasswordSecret))
        {
            var resolvedPassword = _configuration[options.CertificatePasswordSecret];
            options.CertificatePassword = resolvedPassword ?? string.Empty;
        }
        else
        {
            options.CertificatePassword = string.Empty;
        }
    }
}


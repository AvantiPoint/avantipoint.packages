#nullable enable
using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Signing.Gcp;

/// <summary>
/// Extension methods for adding Google Cloud KMS and HSM signing support.
/// </summary>
public static class GcpSigningApplicationExtensions
{
    /// <summary>
    /// Adds Google Cloud KMS support for repository package signing.
    /// </summary>
    public static NuGetApiOptions AddGcpKmsSigning(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<GcpKmsOptions>("Signing:GcpKms");
        options.Services.TryAddSingleton<IRepositorySigningKeyProvider>(provider =>
        {
            var signingOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SigningOptions>>().Value;
            var configuration = provider.GetRequiredService<IConfiguration>();

            if (signingOptions.Mode != SigningMode.GcpKms)
            {
                return null!; // Will be handled by other providers
            }

            return ActivatorUtilities.CreateInstance<GcpKmsRepositorySigningKeyProvider>(
                provider,
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GcpKmsOptions>>(),
                configuration);
        });

        return options;
    }

    /// <summary>
    /// Adds Google Cloud KMS support for repository package signing with configuration.
    /// </summary>
    public static NuGetApiOptions AddGcpKmsSigning(
        this NuGetApiOptions options,
        Action<GcpKmsOptions> configure)
    {
        options.AddGcpKmsSigning();
        options.Services.Configure(configure);
        return options;
    }

    /// <summary>
    /// Adds Google Cloud HSM support for repository package signing.
    /// </summary>
    public static NuGetApiOptions AddGcpHsmSigning(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<GcpHsmOptions>("Signing:GcpHsm");
        options.Services.TryAddSingleton<IRepositorySigningKeyProvider>(provider =>
        {
            var signingOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SigningOptions>>().Value;
            var configuration = provider.GetRequiredService<IConfiguration>();

            if (signingOptions.Mode != SigningMode.GcpHsm)
            {
                return null!; // Will be handled by other providers
            }

            return ActivatorUtilities.CreateInstance<GcpHsmRepositorySigningKeyProvider>(
                provider,
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GcpHsmOptions>>(),
                configuration);
        });

        return options;
    }

    /// <summary>
    /// Adds Google Cloud HSM support for repository package signing with configuration.
    /// </summary>
    public static NuGetApiOptions AddGcpHsmSigning(
        this NuGetApiOptions options,
        Action<GcpHsmOptions> configure)
    {
        options.AddGcpHsmSigning();
        options.Services.Configure(configure);
        return options;
    }
}


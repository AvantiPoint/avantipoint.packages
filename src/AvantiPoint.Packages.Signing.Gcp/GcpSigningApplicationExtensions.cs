#nullable enable
using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;
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
        options.Services.TryAddTransient<GcpKmsRepositorySigningKeyProvider>();
        options.Services.AddScoped<IRepositorySigningKeyProviderServiceProvider, GcpKmsRepositorySigningKeyProviderServiceProvider>();

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
        options.Services.TryAddTransient<GcpHsmRepositorySigningKeyProvider>();
        options.Services.AddScoped<IRepositorySigningKeyProviderServiceProvider, GcpHsmRepositorySigningKeyProviderServiceProvider>();

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


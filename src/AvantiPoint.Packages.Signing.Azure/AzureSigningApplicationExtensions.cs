#nullable enable
using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Signing.Azure;

/// <summary>
/// Extension methods for adding Azure Key Vault signing support.
/// </summary>
public static class AzureSigningApplicationExtensions
{
    /// <summary>
    /// Adds Azure Key Vault support for repository package signing.
    /// </summary>
    public static NuGetApiOptions AddAzureKeyVaultSigning(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<AzureKeyVaultOptions>("Signing:AzureKeyVault");
        options.Services.TryAddTransient<AzureKeyVaultRepositorySigningKeyProvider>();
        options.Services.AddScoped<IRepositorySigningKeyProviderServiceProvider, AzureKeyVaultRepositorySigningKeyProviderServiceProvider>();

        return options;
    }

    /// <summary>
    /// Adds Azure Key Vault support for repository package signing with configuration.
    /// </summary>
    public static NuGetApiOptions AddAzureKeyVaultSigning(
        this NuGetApiOptions options,
        Action<AzureKeyVaultOptions> configure)
    {
        options.AddAzureKeyVaultSigning();
        options.Services.Configure(configure);
        return options;
    }
}


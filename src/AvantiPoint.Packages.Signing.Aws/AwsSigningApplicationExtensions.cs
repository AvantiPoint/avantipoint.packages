#nullable enable
using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Signing.Aws;

/// <summary>
/// Extension methods for adding AWS KMS and Signer signing support.
/// </summary>
public static class AwsSigningApplicationExtensions
{
    /// <summary>
    /// Adds AWS KMS support for repository package signing.
    /// </summary>
    public static NuGetApiOptions AddAwsKmsSigning(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<AwsKmsOptions>("Signing:AwsKms");
        options.Services.TryAddSingleton<IRepositorySigningKeyProvider>(provider =>
        {
            var signingOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SigningOptions>>().Value;
            var configuration = provider.GetRequiredService<IConfiguration>();

            if (signingOptions.Mode != SigningMode.AwsKms)
            {
                return null!; // Will be handled by other providers
            }

            return ActivatorUtilities.CreateInstance<AwsKmsRepositorySigningKeyProvider>(
                provider,
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AwsKmsOptions>>(),
                configuration);
        });

        return options;
    }

    /// <summary>
    /// Adds AWS KMS support for repository package signing with configuration.
    /// </summary>
    public static NuGetApiOptions AddAwsKmsSigning(
        this NuGetApiOptions options,
        Action<AwsKmsOptions> configure)
    {
        options.AddAwsKmsSigning();
        options.Services.Configure(configure);
        return options;
    }

    /// <summary>
    /// Adds AWS Signer support for repository package signing.
    /// </summary>
    public static NuGetApiOptions AddAwsSignerSigning(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<AwsSignerOptions>("Signing:AwsSigner");
        options.Services.TryAddSingleton<IRepositorySigningKeyProvider>(provider =>
        {
            var signingOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SigningOptions>>().Value;
            var configuration = provider.GetRequiredService<IConfiguration>();

            if (signingOptions.Mode != SigningMode.AwsSigner)
            {
                return null!; // Will be handled by other providers
            }

            return ActivatorUtilities.CreateInstance<AwsSignerRepositorySigningKeyProvider>(
                provider,
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AwsSignerOptions>>(),
                configuration);
        });

        return options;
    }

    /// <summary>
    /// Adds AWS Signer support for repository package signing with configuration.
    /// </summary>
    public static NuGetApiOptions AddAwsSignerSigning(
        this NuGetApiOptions options,
        Action<AwsSignerOptions> configure)
    {
        options.AddAwsSignerSigning();
        options.Services.Configure(configure);
        return options;
    }
}


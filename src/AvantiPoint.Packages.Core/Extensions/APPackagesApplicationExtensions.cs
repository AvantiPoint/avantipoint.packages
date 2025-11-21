using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class APPackagesApplicationExtensions
    {
        public static NuGetApiOptions AddFileStorage(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<FileStorageService>());
            return options;
        }

        public static NuGetApiOptions AddFileStorage(
            this NuGetApiOptions options,
            Action<FileSystemStorageOptions> configure)
        {
            options.AddFileStorage();
            options.Services.Configure(configure);
            return options;
        }

        public static NuGetApiOptions AddNullStorage(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<NullStorageService>());
            return options;
        }

        public static NuGetApiOptions AddNullSearch(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<ISearchIndexer>(provider => provider.GetRequiredService<NullSearchIndexer>());
            options.Services.TryAddTransient<ISearchService>(provider => provider.GetRequiredService<NullSearchService>());
            return options;
        }

        public static NuGetApiOptions AddRepositorySigning(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<SigningOptions>("Signing");
            
            // Post-configure to resolve certificate password from secret store
            options.Services.AddSingleton<IPostConfigureOptions<SigningOptions>>(provider =>
            {
                var configuration = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
                return new PostConfigureSigningOptions(configuration);
            });
            
            options.Services.TryAddSingleton<IRepositorySigningKeyProvider>(provider =>
            {
                var signingOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SigningOptions>>().Value;

                if (signingOptions.Mode is null)
                {
                    return new NullSigningKeyProvider();
                }

                return signingOptions.Mode switch
                {
                    SigningMode.SelfSigned => ActivatorUtilities.CreateInstance<SelfSignedRepositorySigningKeyProvider>(provider),
                    SigningMode.StoredCertificate => ActivatorUtilities.CreateInstance<StoredCertificateRepositorySigningKeyProvider>(provider),
                    SigningMode.AzureKeyVault => throw new NotSupportedException(
                        $"Signing mode '{signingOptions.Mode}' requires the AvantiPoint.Packages.Signing.Azure package. " +
                        "Call options.AddAzureKeyVaultSigning() to enable this mode."),
                    SigningMode.AwsKms => throw new NotSupportedException(
                        $"Signing mode '{signingOptions.Mode}' requires the AvantiPoint.Packages.Signing.Aws package. " +
                        "Call options.AddAwsKmsSigning() to enable this mode."),
                    SigningMode.AwsSigner => throw new NotSupportedException(
                        $"Signing mode '{signingOptions.Mode}' requires the AvantiPoint.Packages.Signing.Aws package. " +
                        "Call options.AddAwsSignerSigning() to enable this mode."),
                    SigningMode.GcpKms => throw new NotSupportedException(
                        $"Signing mode '{signingOptions.Mode}' requires the AvantiPoint.Packages.Signing.Gcp package. " +
                        "Call options.AddGcpKmsSigning() to enable this mode."),
                    SigningMode.GcpHsm => throw new NotSupportedException(
                        $"Signing mode '{signingOptions.Mode}' requires the AvantiPoint.Packages.Signing.Gcp package. " +
                        "Call options.AddGcpHsmSigning() to enable this mode."),
                    _ => throw new NotSupportedException($"Signing mode '{signingOptions.Mode}' is not supported.")
                };
            });

            return options;
        }

        public static NuGetApiOptions AddRepositorySigning(
            this NuGetApiOptions options,
            Action<SigningOptions> configure)
        {
            options.AddRepositorySigning();
            options.Services.Configure(configure);
            return options;
        }
    }
}

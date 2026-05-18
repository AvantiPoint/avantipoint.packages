using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Storage;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class APPackagesApplicationExtensions
    {
        /// <summary>
        /// Explicitly adds file system storage support.
        /// File system storage will always be used regardless of Storage:Type configuration.
        /// </summary>
        public static NuGetApiOptions AddFileStorage(this NuGetApiOptions options)
        {
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<FileStorageService>());
            return options;
        }

        /// <summary>
        /// Explicitly adds file system storage support with custom options configuration.
        /// File system storage will always be used regardless of Storage:Type configuration.
        /// </summary>
        public static NuGetApiOptions AddFileStorage(
            this NuGetApiOptions options,
            Action<FileSystemStorageOptions> configure)
        {
            options.AddFileStorage();
            options.Services.Configure(configure);
            return options;
        }

        /// <summary>
        /// Registers file system storage provider for auto-discovery mode.
        /// The provider will be selected based on Storage:Type configuration.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverFileStorage(this NuGetApiOptions options)
        {
            options.Services.AddScoped<IStorageServiceProvider, FileStorageServiceProvider>();
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
            
            options.Services.TryAddTransient<SelfSignedRepositorySigningKeyProvider>();
            options.Services.TryAddTransient<StoredCertificateRepositorySigningKeyProvider>();
            options.Services.AddScoped<IRepositorySigningKeyProviderServiceProvider, SelfSignedRepositorySigningKeyProviderServiceProvider>();
            options.Services.AddScoped<IRepositorySigningKeyProviderServiceProvider, StoredCertificateRepositorySigningKeyProviderServiceProvider>();

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

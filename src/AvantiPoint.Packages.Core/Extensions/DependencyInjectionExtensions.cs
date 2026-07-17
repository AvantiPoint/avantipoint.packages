using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Core.Entities;
using AvantiPoint.Packages.Core.Signing;
using AvantiPoint.Packages.Core.Storage;
using AvantiPoint.Packages.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core
{
    public static partial class DependencyInjectionExtensions
    {
        public static IServiceCollection AddNuGetApiApplication(
            this IServiceCollection services,
            Action<NuGetApiOptions> configureAction)
        {
            var options = new NuGetApiOptions(services);

            services.AddConfiguration();
            services.AddNuGetApiServices();
            services.AddDefaultProviders();

            configureAction(options);

            services.AddTransient<ISearchService>(provider =>
                GetServiceFromProviders<ISearchService>(provider)
                ?? provider.GetRequiredService<DatabaseSearchService>());
            services.AddTransient<ISearchIndexer>(provider =>
                GetServiceFromProviders<ISearchIndexer>(provider)
                ?? provider.GetRequiredService<NullSearchIndexer>());

            services.AddFallbackServices();

            return services;
        }

        /// <summary>
        /// Configures and validates options.
        /// </summary>
        /// <typeparam name="TOptions">The options type that should be added.</typeparam>
        /// <param name="services">The dependency injection container to add options.</param>
        /// <param name="key">
        /// The configuration key that should be used when configuring the options.
        /// If null, the root configuration will be used to configure the options.
        /// </param>
        /// <returns>The dependency injection container.</returns>
        public static IServiceCollection AddNuGetApiOptions<TOptions>(
            this IServiceCollection services,
            string key = null)
            where TOptions : class
        {
            services.AddSingleton<IValidateOptions<TOptions>>(new ValidateNuGetApiOptions<TOptions>(key));
            services.AddSingleton<IConfigureOptions<TOptions>>(provider =>
            {
                var config = provider.GetRequiredService<IConfiguration>();
                if (key != null)
                {
                    config = config.GetSection(key);
                }

                return new BindOptions<TOptions>(config);
            });

            // Options may reference a named connection string instead of an inline value.
            // Resolve it from the root ConnectionStrings section after binding, before validation.
            if (typeof(IConnectionStringOptions).IsAssignableFrom(typeof(TOptions)))
            {
                services.AddSingleton<IPostConfigureOptions<TOptions>>(provider =>
                    new ResolveConnectionStringName<TOptions>(provider.GetRequiredService<IConfiguration>()));
            }

            return services;
        }

        private static void AddConfiguration(this IServiceCollection services)
        {
            services.AddNuGetApiOptions<PackageFeedOptions>();
            services.AddNuGetApiOptions<DatabaseOptions>(nameof(PackageFeedOptions.Database));
            services.AddNuGetApiOptions<FileSystemStorageOptions>(nameof(PackageFeedOptions.Storage));
            services.AddNuGetApiOptions<SearchOptions>(nameof(PackageFeedOptions.Search));
            services.AddSingleton<IValidateOptions<SearchOptions>, ValidateSearchOptions>();
            services.AddNuGetApiOptions<StorageOptions>(nameof(PackageFeedOptions.Storage));
            services.AddNuGetApiOptions<MirrorOptions>(nameof(PackageFeedOptions.Mirror));
            services.AddNuGetApiOptions<LocalCacheOptions>("LocalCache");
            services.AddNuGetApiOptions<RetentionOptions>("Retention");
            services.AddNuGetApiOptions<SigningOptions>("Signing");
        }

        private static void AddNuGetApiServices(this IServiceCollection services)
        {
            // Register TimeProvider for testability (allows mocking time in tests)
            services.TryAddSingleton<TimeProvider>(TimeProvider.System);

            // Secret protection for stored feed credentials. Hosts may register an encrypting
            // implementation (e.g. Data Protection) before calling AddNuGetApiApplication.
            services.TryAddSingleton<ISecretProtector, NullSecretProtector>();

            services.TryAddSingleton<IFrameworkCompatibilityService, FrameworkCompatibilityService>();

            services.TryAddSingleton<NullSearchIndexer>();
            services.TryAddSingleton<NullSearchService>();
            services.TryAddSingleton<SearchDocumentMapper>();
            services.TryAddTransient<IPackageSearchDocumentFactory, PackageSearchDocumentFactory>();
            services.TryAddTransient<ISearchIndexingService, SearchIndexingService>();
            services.AddHostedService<SearchIndexReconciliationHostedService>();
            services.TryAddSingleton<RegistrationBuilder>();
            services.TryAddSingleton<TimeProvider>(TimeProvider.System);
            services.TryAddSingleton<ValidateStartupOptions>();
            // Register CertificateValidationHelper for signing services
            services.TryAddSingleton<Signing.CertificateValidationHelper>();

            services.TryAddTransient<IPackageAuthenticationService, ApiKeyAuthenticationService>();
            services.TryAddTransient<IPackageContentService, DefaultPackageContentService>();
            services.TryAddTransient<IPackageDeletionService, PackageDeletionService>();
            services.TryAddTransient<IPackageIndexingService, PackageIndexingService>();
            services.TryAddTransient<IPackageMetadataService, DefaultPackageMetadataService>();
            services.TryAddTransient<IPackageStorageService, PackageStorageService>();
            services.TryAddTransient<IServiceIndexService, APPackagesServiceIndex>();
            services.TryAddTransient<ISymbolIndexingService, SymbolIndexingService>();
            services.TryAddTransient<ISymbolStorageService, SymbolStorageService>();
            services.TryAddTransient<IVulnerabilityService, VulnerabilityService>();
            services.TryAddTransient<Signing.RepositorySigningCertificateService>();
            services.TryAddSingleton<Signing.ITimestampProviderFactory, Signing.Rfc3161TimestampProviderFactory>();
            services.TryAddTransient<Signing.IPackageSigningService, Signing.PackageSigningService>();
            services.TryAddTransient<Signing.PackageSignatureStripper>();
            services.TryAddSingleton<Signing.NullSigningKeyProvider>();
            services.TryAddTransient<NuGetConfigParser>();

            services.TryAddTransient<DatabaseSearchService>();
            services.TryAddTransient<FileStorageService>();
            services.TryAddTransient<IMirrorService, MirrorService>();
            services.TryAddTransient<ILocalPackageCacheService, LocalPackageCacheService>();
            services.TryAddSingleton<NullStorageService>();
            services.TryAddSingleton<IFeedScope, DefaultFeedScope>();
            services.TryAddTransient<PackageService>();
            services.TryAddTransient<IPackageService>(provider => provider.GetRequiredService<PackageService>());
            services.TryAddScoped<IPackageSourceService, PackageSourceService>();

            // Maintenance services
            services.TryAddTransient<Maintenance.IPackageBackfillStateService, Maintenance.PackageBackfillStateService>();
            services.AddHostedService<Maintenance.RepositoryCommitBackfillService>();
            services.TryAddScoped<Maintenance.IRetentionPolicyService, Maintenance.RetentionPolicyService>();
            services.AddHostedService<Maintenance.RetentionHostedService>();

            // Signing validation
            services.AddHostedService<Signing.SigningStartupValidationService>();

            services.TryAddScoped<IServiceDiscovery, ServiceDiscovery>();
            services.TryAddScoped<IStorageService>(provider => provider.GetRequiredService<IServiceDiscovery>().GetStorageService());
            services.TryAddScoped<IContext>(provider => provider.GetRequiredService<IServiceDiscovery>().GetContext());
            services.TryAddScoped<Signing.IRepositorySigningKeyProvider>(provider => provider.GetRequiredService<IServiceDiscovery>().GetSigningKeyProvider());

            // Register default providers - these are always registered regardless of configuration
            services.AddScoped<IStorageServiceProvider, FileStorageServiceProvider>();
            services.AddScoped<IStorageServiceProvider, NullStorageServiceProvider>();
            services.AddScoped<IRepositorySigningKeyProviderServiceProvider, NullSigningKeyProviderServiceProvider>();

            // Service provider validation
            services.AddHostedService<Discovery.ServiceProviderValidationService>();
        }

        private static void AddDefaultProviders(this IServiceCollection services)
        {
            services.AddProvider((provider, configuration) =>
            {
                if (!configuration.HasSearchType("null")) return null;

                return provider.GetRequiredService<NullSearchService>();
            });

            services.AddProvider((provider, configuration) =>
            {
                if (!configuration.HasSearchType("null")) return null;

                return provider.GetRequiredService<NullSearchIndexer>();
            });

        }

        private static void AddFallbackServices(this IServiceCollection services)
        {
            // AvantiPoint Packages's services have multiple implementations that live side-by-side.
            // The application will choose the implementation using one of two ways:
            //
            // 1. Using the first implementation that was registered in the dependency injection
            //    container. This is the strategy used by applications that embed AvantiPoint Packages.
            // 2. Using "providers". The providers will examine the application's configuration to
            //    determine whether its service implementation is active. Thsi is the strategy used
            //    by the default AvantiPoint Packages application.
            //
            // AvantiPoint Packages has database and search services, but the database services are special
            // in that they may also act as search services. If an application registers the
            // database service first and the search service second, the application should
            // use the search service even though it wasn't registered first. Furthermore,
            // if an application registers a database service without a search service, the
            // database service should be used for search. This effect is achieved by deferring
            // the database search service's registration until the very end.
        }

    }
}

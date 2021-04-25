using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using AvantiPoint.Packages.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core
{
    public static partial class DependencyInjectionExtensions
    {
        private static bool _mirrorsAdded;

        public static IServiceCollection AddNuGetApiApplication(
            this IServiceCollection services,
            Action<NuGetApiOptions> configureAction)
        {
            var options = new NuGetApiOptions(services);

            services.AddConfiguration();
            services.AddNuGetApiServices();
            services.AddDefaultProviders();

            configureAction(options);

            if(!_mirrorsAdded)
                options.AddUpstreamMirrors();

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

            return services;
        }

        public static NuGetApiOptions AddUpstreamMirrors(this NuGetApiOptions options)
        {
            _mirrorsAdded = true;
            var feedOptions = options.Configuration.Get<PackageFeedOptions>();
            foreach((var name, var configuration) in feedOptions.Mirror ?? new MirrorOptions())
            {
                if (!string.IsNullOrEmpty(configuration.Username) && !string.IsNullOrEmpty(configuration.ApiToken))
                    options.AddUpstreamSource(name, configuration.FeedUrl.ToString(), configuration.Username, configuration.ApiToken, configuration.Timeout);
                else
                    options.AddUpstreamSource(name, configuration.FeedUrl.ToString(), configuration.Timeout);
            }

            return options;
        }

        private static void AddConfiguration(this IServiceCollection services)
        {
            services.AddNuGetApiOptions<PackageFeedOptions>();
            services.AddNuGetApiOptions<DatabaseOptions>(nameof(PackageFeedOptions.Database));
            services.AddNuGetApiOptions<FileSystemStorageOptions>(nameof(PackageFeedOptions.Storage));
            services.AddNuGetApiOptions<SearchOptions>(nameof(PackageFeedOptions.Search));
            services.AddNuGetApiOptions<StorageOptions>(nameof(PackageFeedOptions.Storage));
        }

        private static void AddNuGetApiServices(this IServiceCollection services)
        {
            services.TryAddSingleton<IFrameworkCompatibilityService, FrameworkCompatibilityService>();

            services.TryAddSingleton<NullSearchIndexer>();
            services.TryAddSingleton<NullSearchService>();
            services.TryAddSingleton<RegistrationBuilder>();
            services.TryAddSingleton<SystemTime>();
            services.TryAddSingleton<ValidateStartupOptions>();

            services.TryAddTransient<IPackageAuthenticationService, ApiKeyAuthenticationService>();
            services.TryAddTransient<IPackageContentService, DefaultPackageContentService>();
            services.TryAddTransient<IPackageDeletionService, PackageDeletionService>();
            services.TryAddTransient<IPackageIndexingService, PackageIndexingService>();
            services.TryAddTransient<IPackageMetadataService, DefaultPackageMetadataService>();
            services.TryAddTransient<IPackageStorageService, PackageStorageService>();
            services.TryAddTransient<IServiceIndexService, APPackagesServiceIndex>();
            services.TryAddTransient<ISymbolIndexingService, SymbolIndexingService>();
            services.TryAddTransient<ISymbolStorageService, SymbolStorageService>();

            services.TryAddTransient<DatabaseSearchService>();
            services.TryAddTransient<FileStorageService>();
            services.TryAddTransient<MirrorService>();
            services.TryAddTransient<NullMirrorService>();
            services.TryAddSingleton<NullStorageService>();
            services.TryAddTransient<PackageService>();

            services.TryAddTransient(IMirrorServiceFactory);
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

            services.AddProvider<IStorageService>((provider, configuration) =>
            {
                if (configuration.HasStorageType("filesystem"))
                {
                    return provider.GetRequiredService<FileStorageService>();
                }

                if (configuration.HasStorageType("null"))
                {
                    return provider.GetRequiredService<NullStorageService>();
                }

                return null;
            });
        }

        private static void AddFallbackServices(this IServiceCollection services)
        {
            services.TryAddScoped<IContext, NullContext>();

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
            services.TryAddTransient<ISearchIndexer>(provider => provider.GetRequiredService<NullSearchIndexer>());
            services.TryAddTransient<ISearchService>(provider => provider.GetRequiredService<DatabaseSearchService>());
        }

        public static NuGetApiOptions AddUpstreamSource(this NuGetApiOptions options, string name, string serviceIndexUrl, int timeoutInSeconds = 600)
        {
            _mirrorsAdded = true;
            options.Services.AddSingleton<IUpstreamNuGetSource>(sp =>
            {
                var clientFactory = new NuGetClientFactory(HttpClientFactory(timeoutInSeconds), serviceIndexUrl);
                return new UpstreamNuGetSource(name, new NuGetClient(clientFactory));
            });
            return options;
        }

        public static NuGetApiOptions AddUpstreamSource(this NuGetApiOptions options, string name, string serviceIndexUrl, string username, string apiToken, int timeoutInSeconds = 600)
        {
            _mirrorsAdded = true;
            options.Services.AddSingleton<IUpstreamNuGetSource>(sp =>
            {
                var httpClient = HttpClientFactory(timeoutInSeconds);
                var creds = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{apiToken}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
                var clientFactory = new NuGetClientFactory(httpClient, serviceIndexUrl);
                return new UpstreamNuGetSource(name, new NuGetClient(clientFactory));
            });

            return options;
        }

        private static HttpClient HttpClientFactory(int packageDownloadTimeoutSeconds)
        {
            var assembly = Assembly.GetEntryAssembly();
            var assemblyName = assembly.GetName().Name;
            var assemblyVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

            var client = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            });

            client.DefaultRequestHeaders.Add("User-Agent", $"{assemblyName}/{assemblyVersion}");
            client.Timeout = TimeSpan.FromSeconds(packageDownloadTimeoutSeconds);

            return client;
        }

        private static IMirrorService IMirrorServiceFactory(IServiceProvider provider)
        {
            var upstreamSources = provider.GetServices<IUpstreamNuGetSource>();
            return upstreamSources.Any() ?
                provider.GetService<MirrorService>() :
                provider.GetService<NullMirrorService>();
        }
    }
}

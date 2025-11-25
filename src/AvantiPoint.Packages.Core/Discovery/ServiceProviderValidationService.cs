#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core.Discovery;

/// <summary>
/// Validates that all configured service providers are properly configured at startup.
/// This ensures the application fails fast if configuration is invalid.
/// </summary>
internal class ServiceProviderValidationService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PackageFeedOptions _feedOptions;
    private readonly SigningOptions _signingOptions;
    private readonly ILogger<ServiceProviderValidationService> _logger;

    public ServiceProviderValidationService(
        IServiceScopeFactory scopeFactory,
        IOptions<PackageFeedOptions> feedOptions,
        IOptions<SigningOptions> signingOptions,
        ILogger<ServiceProviderValidationService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _feedOptions = feedOptions?.Value ?? throw new ArgumentNullException(nameof(feedOptions));
        _signingOptions = signingOptions?.Value ?? throw new ArgumentNullException(nameof(signingOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_feedOptions.ValidateServiceProviders)
        {
            _logger.LogDebug("Service provider validation is disabled");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Validating configured service providers...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var serviceProvider = scope.ServiceProvider;
            
            // Resolve providers from the scoped service provider
            var storageProviders = serviceProvider.GetServices<IStorageServiceProvider>();
            var contextProviders = serviceProvider.GetServices<IContextServiceProvider>();
            var signingProviders = serviceProvider.GetServices<IRepositorySigningKeyProviderServiceProvider>();

            var storageProvidersDict = ProviderDictionaryBuilder.Build(storageProviders, p => p.Name);
            var contextProvidersDict = ProviderDictionaryBuilder.Build(contextProviders, p => p.Name);
            var signingProvidersDict = ProviderDictionaryBuilder.Build(signingProviders, p => p.Name);

            ValidateStorageProvider(storageProvidersDict);
            ValidateDatabaseProvider(contextProvidersDict);
            ValidateSigningProvider(signingProvidersDict);

            _logger.LogInformation("All service providers validated successfully");
        }
        catch (Exception ex)
        {
            var errorMessage = "Service provider validation failed. Please check your configuration.";
            _logger.LogCritical(ex, errorMessage);
            throw new InvalidOperationException(errorMessage, ex);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private void ValidateStorageProvider(IReadOnlyDictionary<string, IStorageServiceProvider> storageProviders)
    {
        _logger.LogDebug("Validating storage provider...");
        var storageName = _feedOptions.Storage?.Type ?? StorageProviderNames.FileSystem;
        var provider = ResolveProvider(storageProviders, storageName, "storage");
        provider.ValidateConfiguration();
        _logger.LogInformation("Storage provider validated successfully: {StorageType}", storageName);
    }

    private void ValidateDatabaseProvider(IReadOnlyDictionary<string, IContextServiceProvider> contextProviders)
    {
        _logger.LogDebug("Validating database provider...");
        var databaseName = _feedOptions.Database?.Type;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("Database:Type must be configured. Please specify a database provider.");
        }
        var provider = ResolveProvider(contextProviders, databaseName, "database");
        provider.ValidateConfiguration();
        _logger.LogInformation("Database provider validated successfully: {DatabaseType}", databaseName);
    }

    private void ValidateSigningProvider(IReadOnlyDictionary<string, IRepositorySigningKeyProviderServiceProvider> signingProviders)
    {
        _logger.LogDebug("Validating signing provider...");
        var signingName = _signingOptions.Provider ?? SigningProviderNames.Null;
        var provider = ResolveProvider(signingProviders, signingName, "signing");
        provider.ValidateConfiguration();

        if (string.Equals(signingName, SigningProviderNames.Null, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Signing provider validated: signing is disabled");
        }
        else
        {
            _logger.LogInformation("Signing provider validated successfully: {SigningProvider}", signingName);
        }
    }

    private static TProvider ResolveProvider<TProvider>(
        IReadOnlyDictionary<string, TProvider> providers,
        string providerName,
        string providerType)
        where TProvider : class
    {
        if (!providers.TryGetValue(providerName, out var provider))
        {
            throw new InvalidOperationException(
                $"No {providerType} provider registered with name '{providerName}'. " +
                $"Available providers: {string.Join(", ", providers.Keys)}");
        }

        return provider;
    }
}


#nullable enable
using System;
using System.Collections.Generic;
using AvantiPoint.Packages.Core.Signing;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core.Discovery;

internal class ServiceDiscovery : IServiceDiscovery
{
    private readonly IReadOnlyDictionary<string, IStorageServiceProvider> _storageProviders;
    private readonly IReadOnlyDictionary<string, IContextServiceProvider> _contextProviders;
    private readonly IReadOnlyDictionary<string, IRepositorySigningKeyProviderServiceProvider> _signingProviders;
    private readonly PackageFeedOptions _feedOptions;
    private readonly SigningOptions _signingOptions;

    public ServiceDiscovery(
        IEnumerable<IStorageServiceProvider> storageProviders,
        IEnumerable<IContextServiceProvider> dbProviders,
        IEnumerable<IRepositorySigningKeyProviderServiceProvider> signingProviders,
        IOptions<PackageFeedOptions> feedOptions,
        IOptions<SigningOptions> signingOptions)
    {
        _storageProviders = ProviderDictionaryBuilder.Build(storageProviders, p => p.Name);
        _contextProviders = ProviderDictionaryBuilder.Build(dbProviders, p => p.Name);
        _signingProviders = ProviderDictionaryBuilder.Build(signingProviders, p => p.Name);
        _feedOptions = feedOptions.Value;
        _signingOptions = signingOptions.Value;
    }

    public IStorageService GetStorageService(string? name = null)
    {
        var configuredName = name ?? _feedOptions.Storage?.Type ?? StorageProviderNames.FileSystem;
        var provider = ResolveProvider(
            _storageProviders,
            configuredName,
            "storage");
        return provider.GetService();
    }

    public IContext GetContext(string? name = null)
    {
        var configuredName = name ?? _feedOptions.Database?.Type ?? DatabaseProviderNames.Sqlite;
        var provider = ResolveProvider(
            _contextProviders,
            configuredName,
            "database");
        return provider.GetService();
    }

    public IRepositorySigningKeyProvider GetSigningKeyProvider(string? name = null)
    {
        var configuredName = name ?? _signingOptions.Provider ?? SigningProviderNames.Null;
        var provider = ResolveProvider(
            _signingProviders,
            configuredName,
            "signing");
        return provider.GetService();
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

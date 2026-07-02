#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core;

public class PackageSourceService(
    IContext context,
    IOptions<MirrorOptions> mirrorOptions,
    NuGetConfigParser nugetConfigParser,
    ISecretProtector secretProtector) : IPackageSourceService
{
    private const int DefaultTimeoutSeconds = 600;

    public async Task<IReadOnlyList<PackageSource>> GetEnabledUpstreamSourcesAsync(CancellationToken cancellationToken = default)
    {
        var sources = await context.PackageSources
            .AsNoTracking()
            .Where(s => s.IsEnabled && (s.Type == PackageSourceType.Upstream || s.Type == PackageSourceType.Both))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        var result = new List<PackageSource>(sources);
        AppendNuGetConfigSources(result);

        return result;
    }

    public async Task<PackageSource> GetRequiredAsync(int id, CancellationToken cancellationToken = default)
    {
        var source = await context.PackageSources.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (source is null)
        {
            throw new InvalidOperationException($"Package source {id} does not exist.");
        }

        return source;
    }

    public async Task<PackageSource> AddAsync(PackageSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        source.CreatedAt = DateTimeOffset.UtcNow;
        source.LastModifiedAt = source.CreatedAt;
        ProtectCredentials(source);

        context.PackageSources.Add(source);
        await context.SaveChangesAsync(cancellationToken);

        return source;
    }

    public async Task<PackageSource> UpdateAsync(PackageSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var tracked = await context.PackageSources.FirstOrDefaultAsync(s => s.Id == source.Id, cancellationToken);
        if (tracked is null)
        {
            throw new InvalidOperationException($"Package source {source.Id} does not exist.");
        }

        tracked.Name = source.Name;
        tracked.FeedUrl = source.FeedUrl;
        tracked.Type = source.Type;
        tracked.CachingStrategy = source.CachingStrategy;
        tracked.Username = secretProtector.Protect(source.Username);
        tracked.Password = secretProtector.Protect(source.Password);
        tracked.ApiKey = secretProtector.Protect(source.ApiKey);
        tracked.IsEnabled = source.IsEnabled;
        tracked.MirrorSignaturePolicy = source.MirrorSignaturePolicy;
        tracked.Metadata = source.Metadata ?? tracked.Metadata;
        tracked.LastModifiedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return tracked;
    }

    public async Task<PackageSourceMetadata> RefreshMetadataAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        var source = await context.PackageSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (source is null)
        {
            throw new InvalidOperationException($"Package source {sourceId} does not exist.");
        }

        await RefreshMetadataInternalAsync(source, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return source.Metadata;
    }

    public async Task UpdateSyncStateAsync(int sourceId, bool success, string? error, CancellationToken cancellationToken = default)
    {
        var source = await context.PackageSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (source is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        source.LastSyncAttemptAt = now;

        if (success)
        {
            source.LastSyncSuccessAt = now;
            source.LastError = null;
        }
        else
        {
            source.LastError = error;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private void ProtectCredentials(PackageSource source)
    {
        source.Username = secretProtector.Protect(source.Username);
        source.Password = secretProtector.Protect(source.Password);
        source.ApiKey = secretProtector.Protect(source.ApiKey);
    }

    private void AppendNuGetConfigSources(List<PackageSource> sources)
    {
        var options = mirrorOptions.Value;
        if (string.IsNullOrWhiteSpace(options.NuGetConfigPath))
        {
            return;
        }

        var existingNames = new HashSet<string>(sources.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var configSources = nugetConfigParser.LoadSourcesFromConfig(options.NuGetConfigPath);

        foreach (var configSource in configSources)
        {
            if (existingNames.Contains(configSource.Name))
            {
                continue;
            }

            var packageSource = new PackageSource
            {
                Name = configSource.Name,
                FeedUrl = configSource.SourceUrl,
                Type = PackageSourceType.Upstream,
                CachingStrategy = options.DefaultCachingStrategy,
                MirrorSignaturePolicy = options.DefaultSignaturePolicy,
                Username = configSource.Username,
                Password = configSource.Password,
                IsEnabled = true
            };

            sources.Add(packageSource);
            existingNames.Add(configSource.Name);
        }
    }

    private async Task RefreshMetadataInternalAsync(PackageSource source, CancellationToken cancellationToken)
    {
        using var httpClient = PackageSourceHttpClientFactory.Create(source, TimeSpan.FromSeconds(DefaultTimeoutSeconds), secretProtector);
        var clientFactory = new NuGetClientFactory(httpClient, source.FeedUrl);
        var serviceIndex = await clientFactory.CreateServiceIndexClient().GetAsync(cancellationToken);

        source.Metadata ??= new PackageSourceMetadata();
        source.Metadata.Protocol ??= new PackageSourceMetadataProtocol();

        source.Metadata.Protocol.Version = serviceIndex.Version;
        source.Metadata.Protocol.SupportsPackageContent = !string.IsNullOrEmpty(serviceIndex.GetPackageContentResourceUrl());
        source.Metadata.Protocol.SupportsPackageMetadata = !string.IsNullOrEmpty(serviceIndex.GetPackageMetadataResourceUrl());
        source.Metadata.Protocol.SupportsSearch = !string.IsNullOrEmpty(serviceIndex.GetSearchQueryResourceUrl());
        source.Metadata.Protocol.SupportsAutocomplete = !string.IsNullOrEmpty(serviceIndex.GetSearchAutocompleteResourceUrl());
        source.Metadata.Protocol.SupportsSymbolPublish = !string.IsNullOrEmpty(serviceIndex.GetSymbolPublishResourceUrl());
        source.Metadata.Protocol.SupportsVulnerabilityInfo = !string.IsNullOrEmpty(serviceIndex.GetVulnerabilityInfoResourceUrl());
        source.Metadata.Protocol.SupportsRepositorySignatures = !string.IsNullOrEmpty(serviceIndex.GetRepositorySignaturesResourceUrl());
        source.Metadata.Protocol.SupportsReadme = source.Metadata.Protocol.SupportsPackageContent;

        source.LastModifiedAt = DateTimeOffset.UtcNow;
    }
}

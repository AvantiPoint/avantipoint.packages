#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    using PackageIdentity = NuGet.Packaging.Core.PackageIdentity;

    public class MirrorService : IMirrorService
    {
        private const int DefaultTimeoutSeconds = 600;

        private readonly IPackageService _localPackages;
        private readonly IPackageSourceService _packageSourceService;
        private readonly IPackageIndexingService _indexer;
        private readonly IPackageStorageService _storage;
        private readonly ILogger<MirrorService> _logger;
        private readonly ISecretProtector _secretProtector;

        public MirrorService(
            IPackageService localPackages,
            IPackageSourceService packageSourceService,
            IPackageIndexingService indexer,
            IPackageStorageService storage,
            ILogger<MirrorService> logger,
            ISecretProtector secretProtector)
        {
            _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
            _localPackages = localPackages ?? throw new ArgumentNullException(nameof(localPackages));
            _packageSourceService = packageSourceService ?? throw new ArgumentNullException(nameof(packageSourceService));
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<NuGetVersion>?> FindPackageVersionsOrNullAsync(
            string id,
            CancellationToken cancellationToken)
        {
            var sources = await _packageSourceService.GetEnabledUpstreamSourcesAsync(cancellationToken);
            foreach (var source in sources)
            {
                try
                {
                    var versions = await ExecuteWithClientAsync(
                        source,
                        client => client.ListPackageVersionsAsync(id, includeUnlisted: true, cancellationToken),
                        cancellationToken);

                    if (versions == null || !versions.Any())
                    {
                        continue;
                    }

                    var localVersions = await _localPackages.FindVersionsAsync(id, includeUnlisted: true, cancellationToken);
                    return versions.Concat(localVersions).Distinct().ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to fetch package {PackageId} versions from {SourceName}", id, source.Name);
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<Package>?> FindPackagesOrNullAsync(string id, CancellationToken cancellationToken)
        {
            var sources = await _packageSourceService.GetEnabledUpstreamSourcesAsync(cancellationToken);
            foreach (var source in sources)
            {
                try
                {
                    var metadata = await ExecuteWithClientAsync(
                        source,
                        client => client.GetPackageMetadataAsync(id, cancellationToken),
                        cancellationToken);

                    if (metadata == null || !metadata.Any())
                    {
                        continue;
                    }

                    return metadata.Select(ToPackage).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unable to fetch package {PackageId} metadata from {SourceName}", id, source.Name);
                }
            }

            return null;
        }

        public async Task<MirrorOperationResult> MirrorAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (await _localPackages.ExistsAsync(id, version, cancellationToken) ||
                await IsPackageInStorageAsync(id, version, cancellationToken))
            {
                return MirrorOperationResult.AlreadyAvailable;
            }

            var sources = await _packageSourceService.GetEnabledUpstreamSourcesAsync(cancellationToken);
            if (sources.Count == 0)
            {
                return MirrorOperationResult.NotFound;
            }

            foreach (var source in sources)
            {
                var result = await TryMirrorFromSourceAsync(source, id, version, cancellationToken);
                if (result.Found)
                {
                    return result;
                }
            }

            return MirrorOperationResult.NotFound;
        }

        private async Task<MirrorOperationResult> TryMirrorFromSourceAsync(
            PackageSource source,
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken)
        {
            var hasPersistedId = source.Id > 0;

            try
            {
                switch (source.CachingStrategy)
                {
                    case PackageSourceCachingStrategy.ProxyOnly:
                        {
                            var proxiedStream = await DownloadPackageAsync(source, id, version, cancellationToken);
                            if (proxiedStream == null)
                            {
                                return MirrorOperationResult.NotFound;
                            }

                            if (hasPersistedId)
                            {
                                await _packageSourceService.UpdateSyncStateAsync(source.Id, success: true, error: null, cancellationToken);
                            }

                            return MirrorOperationResult.Proxied(source, proxiedStream);
                        }

                    default:
                        {
                            var packageStream = await DownloadPackageAsync(source, id, version, cancellationToken);
                            if (packageStream == null)
                            {
                                return MirrorOperationResult.NotFound;
                            }

                            try
                            {
                                var ingestionContext = new PackageIngestionContext
                                {
                                    Origin = source.CachingStrategy == PackageSourceCachingStrategy.CacheOnly
                                        ? PackageOrigin.Cached
                                        : PackageOrigin.Mirrored,
                                    PackageSourceId = hasPersistedId ? source.Id : null,
                                    MirrorSignaturePolicy = source.MirrorSignaturePolicy,
                                    CachingStrategy = source.CachingStrategy,
                                    SkipSearchIndexing = source.CachingStrategy == PackageSourceCachingStrategy.CacheOnly,
                                    SkipDatabasePersistence = source.CachingStrategy == PackageSourceCachingStrategy.CacheOnly,
                                    ApplyPublishSignaturePolicy = false
                                };

                                var indexingResult = await _indexer.IndexAsync(packageStream, ingestionContext, cancellationToken);
                                if (indexingResult.Status == PackageIndexingStatus.Success ||
                                    indexingResult.Status == PackageIndexingStatus.PackageAlreadyExists)
                                {
                                    if (hasPersistedId)
                                    {
                                        await _packageSourceService.UpdateSyncStateAsync(source.Id, success: true, error: null, cancellationToken);
                                    }

                                    return MirrorOperationResult.Stored(source);
                                }

                                _logger.LogWarning(
                                    "Indexing package {PackageId} {PackageVersion} from {SourceName} returned status {Status}",
                                    id,
                                    version,
                                    source.Name,
                                    indexingResult.Status);
                            }
                            finally
                            {
                                await packageStream.DisposeAsync();
                            }
                            break;
                        }
                }
            }
            catch (PackageNotFoundException)
            {
                _logger.LogWarning(
                    "Package {PackageId} {PackageVersion} was not found on {SourceName}",
                    id,
                    version,
                    source.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to mirror package {PackageId} {PackageVersion} from {SourceName}",
                    id,
                    version,
                    source.Name);
                if (hasPersistedId)
                {
                    await _packageSourceService.UpdateSyncStateAsync(source.Id, success: false, error: ex.Message, cancellationToken);
                }
            }

            return MirrorOperationResult.NotFound;
        }

        private async Task<T> ExecuteWithClientAsync<T>(
            PackageSource source,
            Func<NuGetClient, Task<T>> action,
            CancellationToken cancellationToken)
        {
            using var httpClient = PackageSourceHttpClientFactory.Create(source, TimeSpan.FromSeconds(DefaultTimeoutSeconds), _secretProtector);
            var factory = new NuGetClientFactory(httpClient, source.FeedUrl);
            var client = new NuGetClient(factory);

            return await action(client);
        }

        private async Task<Stream?> DownloadPackageAsync(
            PackageSource source,
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken)
        {
            using var httpClient = PackageSourceHttpClientFactory.Create(source, TimeSpan.FromSeconds(DefaultTimeoutSeconds), _secretProtector);
            var factory = new NuGetClientFactory(httpClient, source.FeedUrl);
            var client = new NuGetClient(factory);

            using var remoteStream = await client.DownloadPackageAsync(id, version, cancellationToken);
            if (remoteStream == null || remoteStream == Stream.Null)
            {
                return null;
            }

            return await remoteStream.AsTemporaryFileStreamAsync();
        }

        private async Task<bool> IsPackageInStorageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            try
            {
                await using var stream = await _storage.GetPackageStreamAsync(id, version, cancellationToken);
                return stream != null;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
        }

        private Package ToPackage(PackageMetadata metadata)
        {
            return new Package
            {
                Id = metadata.PackageId,
                Version = metadata.ParseVersion(),
                Authors = ParseAuthors(metadata.Authors),
                Description = metadata.Description,
                HasReadme = false,
                Language = metadata.Language,
                Listed = metadata.IsListed(),
                MinClientVersion = metadata.MinClientVersion,
                Published = metadata.Published.UtcDateTime,
                RequireLicenseAcceptance = metadata.RequireLicenseAcceptance,
                Summary = metadata.Summary,
                Title = metadata.Title,
                IconUrl = ParseUri(metadata.IconUrl),
                LicenseUrl = ParseUri(metadata.LicenseUrl),
                ProjectUrl = ParseUri(metadata.ProjectUrl),
                PackageTypes = new List<PackageType>(),
                RepositoryUrl = null,
                RepositoryType = null,
                Tags = metadata.Tags.ToArray(),
                Dependencies = FindDependencies(metadata),
                PackageDownloads = new List<PackageDownload>(),
            };
        }

        private Uri? ParseUri(string? uriString)
        {
            if (uriString == null) return null;

            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                return null;
            }

            return uri;
        }

        private string[] ParseAuthors(string authors)
        {
            if (string.IsNullOrEmpty(authors)) return Array.Empty<string>();

            return authors
                .Split(new[] { ',', ';', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .ToArray();
        }

        private List<PackageDependency> FindDependencies(PackageMetadata package)
        {
            if ((package.DependencyGroups?.Count ?? 0) == 0)
            {
                return new List<PackageDependency>();
            }

            return (package.DependencyGroups ?? Enumerable.Empty<DependencyGroupItem>())
                .SelectMany(FindDependenciesFromDependencyGroup)
                .ToList();
        }

        private IEnumerable<PackageDependency> FindDependenciesFromDependencyGroup(DependencyGroupItem group)
        {
            if ((group.Dependencies?.Count ?? 0) == 0)
            {
                return new[]
                {
                    new PackageDependency
                    {
                        Id = null,
                        VersionRange = null,
                        TargetFramework = group.TargetFramework
                    }
                };
            }

            return (group.Dependencies ?? Enumerable.Empty<DependencyItem>()).Select(d => new PackageDependency
            {
                Id = d.Id,
                VersionRange = d.Range,
                TargetFramework = group.TargetFramework
            });
        }
    }
}

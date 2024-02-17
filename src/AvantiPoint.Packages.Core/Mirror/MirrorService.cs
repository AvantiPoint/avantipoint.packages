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
        private readonly IPackageService _localPackages;
        private readonly IEnumerable<IUpstreamNuGetSource> _upstreamSources;
        private readonly IPackageIndexingService _indexer;
        private readonly ILogger<MirrorService> _logger;

        public MirrorService(
            IPackageService localPackages,
            IEnumerable<IUpstreamNuGetSource> upstreamSources,
            IPackageIndexingService indexer,
            ILogger<MirrorService> logger)
        {
            _localPackages = localPackages ?? throw new ArgumentNullException(nameof(localPackages));
            _upstreamSources = upstreamSources ?? Array.Empty<IUpstreamNuGetSource>();
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<NuGetVersion>> FindPackageVersionsOrNullAsync(
            string id,
            CancellationToken cancellationToken)
        {
            var upstreamVersions = await RunOrNull(
                id,
                "versions",
                x => x.ListPackageVersionsAsync(id, includeUnlisted: true, cancellationToken));

            if (upstreamVersions == null || !upstreamVersions.Any())
            {
                return null;
            }

            // Merge the local package versions into the upstream package versions.
            var localVersions = await _localPackages.FindVersionsAsync(id, includeUnlisted: true, cancellationToken);

            return upstreamVersions.Concat(localVersions).Distinct().ToList();
        }

        public async Task<IReadOnlyList<Package>> FindPackagesOrNullAsync(string id, CancellationToken cancellationToken)
        {
            var items = await RunOrNull(
                id,
                "metadata",
                x => x.GetPackageMetadataAsync(id, cancellationToken));

            if (items == null || !items.Any())
            {
                return null;
            }

            return items.Select(ToPackage).ToList();
        }

        public async Task MirrorAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (await _localPackages.ExistsAsync(id, version, cancellationToken))
            {
                return;
            }

            _logger.LogInformation(
                "Package {PackageId} {PackageVersion} does not exist locally. Indexing from upstream feed...",
                id,
                version);

            await IndexFromSourceAsync(id, version, cancellationToken);

            _logger.LogInformation(
                "Finished indexing {PackageId} {PackageVersion} from the upstream feed",
                id,
                version);
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

        private Uri ParseUri(string uriString)
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
            if (string.IsNullOrEmpty(authors)) return new string[0];

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

            return package.DependencyGroups
                .SelectMany(FindDependenciesFromDependencyGroup)
                .ToList();
        }

        private IEnumerable<PackageDependency> FindDependenciesFromDependencyGroup(DependencyGroupItem group)
        {
            // AvantiPoint Packages stores a dependency group with no dependencies as a package dependency
            // with no package id nor package version.
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

            return group.Dependencies.Select(d => new PackageDependency
            {
                Id = d.Id,
                VersionRange = d.Range,
                TargetFramework = group.TargetFramework
            });
        }

        private async Task<T> RunOrNull<T>(string id, string data, Func<NuGetClient, Task<T>> func)
            where T : class
        {
            foreach(var source in _upstreamSources)
            {
                var result = await RunOrNull(source, id, data, func);
                if (result != null)
                    return result;
            }

            return null;
        }

        private async Task<T> RunOrNull<T>(IUpstreamNuGetSource source, string id, string data, Func<NuGetClient, Task<T>> func)
            where T : class
        {
            try
            {
                return await func(source.Client);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Unable to mirror package {id}'s upstream {data} from {source.Name}");
                return null;
            }
        }

        private async Task IndexFromSourceAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation(
                "Attempting to mirror package {PackageId} {PackageVersion}...",
                id,
                version);

            Stream packageStream = null;

            try
            {
                foreach(var source in _upstreamSources)
                {
                    using var stream = await TryDownloadPackage(source, id, version, cancellationToken);
                    if (stream != null && stream != Stream.Null)
                    {
                        packageStream = await stream.AsTemporaryFileStreamAsync();

                        _logger.LogInformation(
                            $"Downloaded package {id} {version}, indexing...");
                        break;
                    }
                }

                if(packageStream is null)
                {
                    _logger.LogInformation($"Could not find the package {id} {version} on any of the upstream sources.");
                    return;
                }

                var result = await _indexer.IndexAsync(packageStream, cancellationToken);

                _logger.LogInformation(
                    "Finished indexing package {PackageId} {PackageVersion} with result {Result}",
                    id,
                    version,
                    result);
            }
            catch (PackageNotFoundException)
            {
                _logger.LogWarning(
                    "Failed to download package {PackageId} {PackageVersion}",
                    id,
                    version);

                return;
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    $"Failed to mirror package {id} {version}");
            }
            finally
            {
                packageStream?.Dispose();
            }
        }

        private async Task<Stream> TryDownloadPackage(IUpstreamNuGetSource source, string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Stream.Null;

            try
            {
                return await source.Client.DownloadPackageAsync(id, version, cancellationToken);
            }
            catch (PackageNotFoundException)
            {
                _logger.LogWarning(
                    $"Failed to download package {id} {version} from {source.Name}");

                return Stream.Null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    $"Failed to mirror package {id} {version} from {source.Name}");
                return Stream.Null;
            }
        }
    }
}

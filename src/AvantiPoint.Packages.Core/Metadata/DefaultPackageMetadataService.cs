using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.Protocol.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using PackageDependencyInfo = AvantiPoint.Packages.Protocol.Models.PackageDependencyInfo;

namespace AvantiPoint.Packages.Core
{
    /// <inheritdoc />
    public class DefaultPackageMetadataService : IPackageMetadataService
    {
        private readonly IMirrorService _mirror;
        private readonly IPackageService _packages;
        private readonly RegistrationBuilder _builder;
        private readonly IContext _context;
        private readonly IUrlGenerator _urlGenerator;
        private readonly SearchOptions _searchOptions;
        private readonly IFeedScope _feedScope;
        private readonly ILogger<DefaultPackageMetadataService> _logger;

        public DefaultPackageMetadataService(
            IContext context,
            IMirrorService mirror,
            IPackageService packages,
            RegistrationBuilder builder,
            IUrlGenerator urlGenerator,
            IOptions<SearchOptions> searchOptions,
            IFeedScope feedScope,
            ILogger<DefaultPackageMetadataService> logger = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _urlGenerator = urlGenerator;
            _searchOptions = searchOptions?.Value ?? throw new ArgumentNullException(nameof(searchOptions));
            _feedScope = feedScope ?? throw new ArgumentNullException(nameof(feedScope));
            _logger = logger ?? NullLogger<DefaultPackageMetadataService>.Instance;
        }

        public async Task<NuGetApiRegistrationIndexResponse> GetRegistrationIndexOrNullAsync(
            string packageId,
            CancellationToken cancellationToken = default)
        {
            // Default behavior includes all packages (SemVer1 + SemVer2)
            return await GetRegistrationIndexOrNullAsync(packageId, includeSemVer2: true, cancellationToken);
        }

        public async Task<NuGetApiRegistrationIndexResponse> GetRegistrationIndexOrNullAsync(
            string packageId,
            bool includeSemVer2,
            CancellationToken cancellationToken = default)
        {
            var packages = await FindPackagesOrNullAsync(packageId, cancellationToken);
            if (packages == null)
            {
                return null;
            }

            // Filter packages based on SemVer level
            var filteredPackages = FilterPackagesBySemVer(packages, includeSemVer2);
            if (!filteredPackages.Any())
            {
                return null;
            }

            return _builder.BuildIndex(
                new PackageRegistration(
                    packageId,
                    filteredPackages));
        }

        public async Task<RegistrationLeafResponse> GetRegistrationLeafOrNullAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken = default)
        {
            // Default behavior includes all packages (SemVer1 + SemVer2)
            return await GetRegistrationLeafOrNullAsync(id, version, includeSemVer2: true, cancellationToken);
        }

        public async Task<RegistrationLeafResponse> GetRegistrationLeafOrNullAsync(
            string id,
            NuGetVersion version,
            bool includeSemVer2,
            CancellationToken cancellationToken = default)
        {
            // Allow read-through caching to happen if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            var package = await _packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (package == null)
            {
                return null;
            }

            // Filter by SemVer level - if this is a SemVer2 package and includeSemVer2 is false, return null
            if (!includeSemVer2 && Utilities.SemVerHelper.IsSemVer2(package))
            {
                return null;
            }

            return _builder.BuildLeaf(package);
        }

        private async Task<IReadOnlyList<Package>> FindPackagesOrNullAsync(
            string packageId,
            CancellationToken cancellationToken)
        {
            var localPackages = await _packages.FindAsync(packageId, includeUnlisted: true, cancellationToken);
            var filteredLocal = localPackages
                .Where(p => PackageOriginFilter.IsIncludedInDiscovery(p, _searchOptions))
                .ToList();

            if (!_searchOptions.IncludeMirroredPackages)
            {
                return filteredLocal.Count > 0 ? filteredLocal : null;
            }

            var upstreamPackages = await _mirror.FindPackagesOrNullAsync(packageId, cancellationToken);
            if (upstreamPackages == null)
            {
                return filteredLocal.Count > 0 ? filteredLocal : null;
            }

            // Merge the local packages into the upstream packages.
            var result = upstreamPackages.ToDictionary(p => new PackageIdentity(p.Id, p.Version));
            var local = filteredLocal.ToDictionary(p => new PackageIdentity(p.Id, p.Version));

            foreach (var localPackage in local)
            {
                result[localPackage.Key] = localPackage.Value;
            }

            return result.Values.ToList();
        }

        public async Task<PackageInfoCollection> GetPackageInfo(
            string packageId,
            string version = default,
            CancellationToken cancellationToken = default)
        {
            var result = await PackageOriginFilter.ApplyDiscoveryFilter(
                    _context.Packages
                        .Include(x => x.Dependencies)
                        .Include(x => x.PackageTypes)
                        .Include(x => x.TargetFrameworks)
                        .Include(x => x.PackageSource)
                        .Where(x => x.FeedId == _feedScope.FeedId)
                        .Where(x => x.Id.ToLower() == packageId.ToLower()),
                    _searchOptions)
                .ToListAsync(cancellationToken);

            var liveUpstreamIdentities = new HashSet<PackageIdentity>();
            DateTimeOffset? upstreamMetadataFetchedAt = null;
            if ((_searchOptions.CheckUpstreamVersionsForKnownPackages
                    && _searchOptions.IncludeMirroredPackages && result.Count > 0)
                || (_searchOptions.EnableUpstreamSearch
                    && (result.Count == 0 || !ContainsRequestedVersion(result, version))))
            {
                using var timeout = new CancellationTokenSource(_searchOptions.UpstreamSearchTimeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeout.Token);

                try
                {
                    var upstreamPackages = await _mirror.FindPackagesOrNullAsync(packageId, linked.Token);
                    if (upstreamPackages is not null)
                    {
                        upstreamMetadataFetchedAt = DateTimeOffset.UtcNow;
                        var identities = result
                            .Select(package => new PackageIdentity(package.Id, package.Version))
                            .ToHashSet();

                        foreach (var upstreamPackage in upstreamPackages)
                        {
                            var identity = new PackageIdentity(upstreamPackage.Id, upstreamPackage.Version);
                            if (!identities.Add(identity))
                            {
                                continue;
                            }

                            liveUpstreamIdentities.Add(identity);
                            result.Add(upstreamPackage);
                        }
                    }
                }
                catch (OperationCanceledException) when (
                    !cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Live upstream metadata lookup for {PackageId} exceeded {Timeout}",
                        packageId,
                        _searchOptions.UpstreamSearchTimeout);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        exception,
                        "Live upstream metadata lookup for {PackageId} failed; returning local metadata",
                        packageId);
                }
            }

            if (result.Count == 0 || !ContainsRequestedVersion(result, version))
            {
                return new PackageInfoCollection();
            }

            // Get all unique dependency package IDs
            var allDependencyIds = result
                .SelectMany(x => x.Dependencies.Select(d => d.Id))
                .Distinct()
                .ToList();

            // Check which dependencies exist locally in a single query
            var localDependencies = await _context.Packages
                .Where(p => p.FeedId == _feedScope.FeedId)
                .Where(p => allDependencyIds.Contains(p.Id))
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            var localDependencySet = new HashSet<string>(localDependencies, StringComparer.OrdinalIgnoreCase);

            // Get download counts for all versions in one query
            var persistedKeys = result
                .Where(package => package.Key > 0)
                .Select(package => package.Key)
                .ToList();
            var downloadDict = new Dictionary<int, int>();
            if (persistedKeys.Count > 0)
            {
                var downloadCounts = await _context.PackageDownloads
                    .Where(download => persistedKeys.Contains(download.PackageKey))
                    .GroupBy(download => download.PackageKey)
                    .Select(group => new { PackageKey = group.Key, Count = group.Count() })
                    .ToListAsync(cancellationToken);

                downloadDict = downloadCounts.ToDictionary(item => item.PackageKey, item => item.Count);
            }

            var packageInfo = string.IsNullOrEmpty(version) ? new PackageInfoCollection() : new PackageInfoCollection(version);
            packageInfo.Versions = result
                .Select(x =>
                {
                    var identity = new PackageIdentity(x.Id, x.Version);
                    var isLiveUpstream = liveUpstreamIdentities.Contains(identity);
                    return new PackageInfo
                    {
                        PackageId = x.Id,
                        Authors = x.Authors,
                        Dependencies = x.Dependencies
                            .GroupBy(x => x.TargetFramework)
                            .ToDictionary(x => x.Key, x => x.Select(d => new PackageDependencyInfo
                            {
                                PackageId = d.Id,
                                VersionRange = $"({VersionHelper.GetFormattedVersionConstraint(d.VersionRange)})",
                                IsLocalDependency = localDependencySet.Contains(d.Id)
                            })),
                        Description = x.Description,
#pragma warning disable CS0618
                        Downloads = (downloadDict.ContainsKey(x.Key) ? downloadDict[x.Key] : 0) + x.Downloads,
#pragma warning restore CS0618
                        HasReadme = x.HasReadme,
                        IsListed = x.Listed,
                        IsPrerelease = x.IsPrerelease,
                        IsDevelopmentDependency = x.IsDevelopmentDependency,
                        IsTool = x.IsTool,
                        IsTemplate = x.PackageTypes.Any(x => x.Name.Equals("Template", StringComparison.OrdinalIgnoreCase)),
                        IsDeprecated = x.IsDeprecated,
                        DeprecationReasons = x.DeprecationReasons ?? [],
                        DeprecationMessage = x.DeprecationMessage,
                        DeprecatedAlternatePackageId = x.DeprecatedAlternatePackageId,
                        DeprecatedAlternatePackageVersionRange = x.DeprecatedAlternatePackageVersionRange,
                        IconUrl = x.HasEmbeddedIcon ? _urlGenerator.GetPackageIconDownloadUrl(x.Id, x.Version) : x.IconUrlString,
                        LicenseUrl = x.LicenseUrlString,
                        LicenseExpression = x.LicenseExpression,
                        HasEmbeddedLicense = x.HasEmbeddedLicense,
                        ProjectUrl = x.ProjectUrlString,
                        Published = Utilities.PackageDateTime.AsUtcOffset(x.Published),
                        ReleaseNotes = x.ReleaseNotes,
                        RequireLicenseAcceptance = x.RequireLicenseAcceptance,
                        RepositoryType = x.RepositoryType,
                        RepositoryUrl = x.RepositoryUrlString,
                        Summary = x.Summary,
                        Tags = x.Tags,
                        Version = x.Version,
                        TargetFrameworks = (x.TargetFrameworks ?? [])
                            .Select(f => f.Moniker)
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        Origin = isLiveUpstream ? "Upstream" : x.Origin.ToString(),
                        PackageSourceName = x.PackageSource?.Name,
                        PackageSourceLastSyncSuccessAt = x.PackageSource?.LastSyncSuccessAt,
                        MetadataFetchedAt = isLiveUpstream ? upstreamMetadataFetchedAt : null,
                        UpstreamCachingStrategy = isLiveUpstream
                            ? x.PackageSource?.CachingStrategy.ToString()
                            : null,
                    };
                })
                .OrderByDescending(x => x.Version)
                .ToList();
            return packageInfo;
        }

        private static bool ContainsRequestedVersion(IReadOnlyCollection<Package> packages, string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return true;
            }

            if (!NuGetVersion.TryParse(version, out var requestedVersion))
            {
                return false;
            }

            return packages.Any(package => package.Version == requestedVersion);
        }

        private IReadOnlyList<Package> FilterPackagesBySemVer(IReadOnlyList<Package> packages, bool includeSemVer2)
        {
            if (includeSemVer2)
            {
                // Include all packages
                return packages;
            }

            // Filter out SemVer2 packages
            return packages.Where(p => !Utilities.SemVerHelper.IsSemVer2(p)).ToList();
        }
    }
}

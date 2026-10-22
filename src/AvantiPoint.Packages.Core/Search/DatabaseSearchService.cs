using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

#nullable enable

namespace AvantiPoint.Packages.Core
{
    public class DatabaseSearchService : ISearchService
    {
        private readonly IContext _context;
        private readonly IFrameworkCompatibilityService _frameworks;
        private readonly IUrlGenerator _url;
        private readonly SearchOptions _searchOptions;
        private readonly IFeedScope _feedScope;

        public DatabaseSearchService(
            IContext context,
            IFrameworkCompatibilityService frameworks,
            IUrlGenerator url,
            IOptions<SearchOptions> searchOptions,
            IFeedScope feedScope)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _searchOptions = searchOptions?.Value ?? throw new ArgumentNullException(nameof(searchOptions));
            _feedScope = feedScope ?? throw new ArgumentNullException(nameof(feedScope));
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var count = await SearchCountAsync(request, cancellationToken);
            var frameworkGroups = FrameworkSearchFilter.GetMatchGroups(request, _frameworks);
            IQueryable<Package> baseQuery = _context.Packages
                .AsNoTracking()
                .Where(p => p.FeedId == _feedScope.FeedId);

            // Apply search filters (e.g., query, prerelease, package type, frameworks)
            baseQuery = AddSearchFilters(
                baseQuery,
                request.Query,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworkGroups,
                request.FrameworkFilterMode);

            // Step 1: Get distinct package IDs and their total downloads in a single query
            // Use a subquery to calculate downloads once per package ID instead of per version
            var packageDownloads = await PackageOriginFilter.ApplyDiscoveryFilter(
                    _context.Packages
                        .AsNoTracking()
                        .Where(p => p.FeedId == _feedScope.FeedId)
                        .Where(p => baseQuery.Select(bp => bp.Id).Contains(p.Id)),
                    _searchOptions)
                .GroupBy(p => p.Id)
                .Select(g => new
                {
                    PackageId = g.Key,
                    TotalDownloads = g.Sum(p => (long)p.PackageDownloads.Count)
                })
                .ToListAsync(cancellationToken);

            var downloadDict = packageDownloads.ToDictionary(x => x.PackageId, x => x.TotalDownloads);

            // Get distinct packages by Id, selecting the latest version with their types
            // First, get the data without ordering
            var latestPackagesUnordered = await baseQuery
                .Include(p => p.PackageTypes)
                .GroupBy(p => p.Id)
                .Select(g => g.OrderByDescending(p => p.Published).First())
                .ToListAsync(cancellationToken);

            // Then order in memory using the download counts and apply pagination
            var latestPackagesQuery = latestPackagesUnordered
                .OrderByDescending(p => downloadDict.ContainsKey(p.Id) ? downloadDict[p.Id] : 0)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToList();

            // Get all package IDs we need versions for
            var packageIds = latestPackagesQuery.Select(p => p.Id).ToList();

            // Fetch all versions for all packages in a single query
            var allVersionsQuery = PackageOriginFilter.ApplyDiscoveryFilter(
                _context.Packages
                    .AsNoTracking()
                    .Where(p => p.FeedId == _feedScope.FeedId)
                    .Where(p => packageIds.Contains(p.Id)),
                _searchOptions);

            if (!request.IncludePrerelease)
            {
                allVersionsQuery = allVersionsQuery.Where(p => p.IsPrerelease == false);
            }

            // Group versions by package ID with download counts in one query
            var versionsByPackage = await allVersionsQuery
                .GroupBy(p => p.Id)
                .Select(g => new
                {
                    PackageId = g.Key,
                    Versions = g.Select(p => new VersionInfo
                    {
                        Version = p.Version,
                        Downloads = p.PackageDownloads.Count
                    }).ToList()
                })
                .ToListAsync(cancellationToken);

            var versionsDict = versionsByPackage.ToDictionary(x => x.PackageId, x => x.Versions);

            // NuGet search results describe the selected/latest version, so include only advisory
            // ranges that actually cover that version. Keep publisher verification unknown: a
            // locally stored package is not proof that its owner controls a reserved prefix.
            var normalizedPackageIds = packageIds
                .Select(static packageId => packageId.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var vulnerabilityRows = normalizedPackageIds.Count == 0
                ? []
                : await _context.PackageVulnerabilities
                    .AsNoTracking()
                    .Include(static vulnerability => vulnerability.Vulnerability)
                    .Where(vulnerability => normalizedPackageIds.Contains(vulnerability.PackageId))
                    .ToListAsync(cancellationToken);
            var vulnerabilitiesByPackage = vulnerabilityRows
                .GroupBy(static vulnerability => vulnerability.PackageId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    static group => group.Key,
                    static group => group.ToList(),
                    StringComparer.OrdinalIgnoreCase);

            // Step 2: Build SearchResult list
            var data = new List<SearchResult>();
            foreach (var pkg in latestPackagesQuery)
            {
                var totalDownloads = downloadDict.ContainsKey(pkg.Id) ? downloadDict[pkg.Id] : 0;
                var versions = versionsDict.ContainsKey(pkg.Id) 
                    ? versionsDict[pkg.Id] 
                    : new List<VersionInfo>();

                data.Add(new SearchResult
                {
                    PackageId = pkg.Id,
                    Version = pkg.Version.ToFullString(),
                    Description = pkg.Description,
                    Authors = pkg.Authors ?? [],
                    IconUrl = pkg.HasEmbeddedIcon
                        ? _url.GetPackageIconDownloadUrl(pkg.Id, pkg.Version)
                        : pkg.IconUrlString,
                    LicenseUrl = GetLicenseUrl(new PackageSearchQueryResult(
                        pkg.Id,
                        pkg.Version,
                        pkg.Description,
                        pkg.Authors ?? [],
                        pkg.HasEmbeddedIcon,
                        pkg.HasEmbeddedLicense,
                        pkg.IconUrlString,
                        pkg.LicenseUrlString,
                        pkg.ProjectUrlString,
                        pkg.Published,
                        pkg.Summary,
                        pkg.Tags ?? [],
                        pkg.Title,
                        totalDownloads,
                        [.. pkg.PackageTypes.Select(pt => new SearchResultPackageType { Name = pt.Name })]
                    )),
                    ProjectUrl = pkg.ProjectUrlString,
                    RegistrationIndexUrl = _url.GetRegistrationIndexUrl(pkg.Id),
                    Published = Utilities.PackageDateTime.AsUtcOffset(pkg.Published),
                    Summary = pkg.Summary,
                    Tags = pkg.Tags ?? [],
                    Title = pkg.Title,
                    TotalDownloads = totalDownloads,
                    Verified = null,
                    Vulnerabilities = vulnerabilitiesByPackage.TryGetValue(pkg.Id, out var packageVulnerabilities)
                        ? [.. packageVulnerabilities
                            .Where(vulnerability => VersionRange.TryParse(vulnerability.VersionRange, out var range)
                                                    && range.Satisfies(pkg.Version))
                            .Select(static vulnerability => new PackageVulnerabilityInfo
                            {
                                Url = vulnerability.Vulnerability.AdvisoryUrl,
                                Severity = vulnerability.Vulnerability.Severity,
                                Versions = vulnerability.VersionRange,
                            })]
                        : [],
                    PackageTypes = [.. pkg.PackageTypes.Select(pt => new SearchResultPackageType { Name = pt.Name })],
                    Versions = [.. versions
                        .OrderByDescending(v => v.Version)
                        .Select(v => new SearchResultVersion
                        {
                            Version = v.Version.ToFullString(),
                            Downloads = v.Downloads,
                            RegistrationLeafUrl = _url.GetRegistrationLeafUrl(pkg.Id, v.Version),
                        })]
                });
            }

            return new SearchResponse
            {
                TotalHits = count,
                Data = data,
                Context = SearchContext.Default(_url.GetPackageMetadataResourceUrl())
            };
        }

        public async Task<AutocompleteResponse> AutocompleteAsync(
            AutocompleteRequest request,
            CancellationToken cancellationToken)
        {
            IQueryable<Package> search = _context.Packages
                .AsNoTracking()
                .Where(p => p.FeedId == _feedScope.FeedId);

            if (!string.IsNullOrEmpty(request.Query))
            {
                var query = request.Query.ToLower();
                search = search.Where(p => p.Id.ToLower().Contains(query));
            }

            // Query is already applied above with case-insensitive Id.ToLower().Contains.
            // Re-passing it through AddSearchFilters uses Id.Contains, which SQLite translates
            // to case-sensitive instr() (column NOCASE is ignored), so "app" would not match
            // "AppPortal.*". Keep listed/prerelease/semver/type/origin filters only.
            search = AddSearchFilters(
                search,
                searchQuery: null,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworkGroups: null);

            // Optimize: Get package IDs first, then calculate download counts separately
            var packageIds = await search
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Get download counts for all matched packages in a single query
            var downloadCounts = await PackageOriginFilter.ApplyDiscoveryFilter(
                    _context.Packages
                        .AsNoTracking()
                        .Where(p => p.FeedId == _feedScope.FeedId)
                        .Where(p => packageIds.Contains(p.Id)),
                    _searchOptions)
                .GroupBy(p => p.Id)
                .Select(g => new
                {
                    PackageId = g.Key,
                    Downloads = g.Sum(p => (long)p.PackageDownloads.Count)
                })
                .OrderByDescending(x => x.Downloads)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            var results = downloadCounts.Select(x => x.PackageId).ToList();

            return new AutocompleteResponse
            {
                TotalHits = packageIds.Count,
                Data = results,
                Context = AutocompleteContext.Default
            };
        }

        public async Task<AutocompleteResponse> ListPackageVersionsAsync(
            VersionsRequest request,
            CancellationToken cancellationToken)
        {
            var packageId = request.PackageId.ToLower();
            IQueryable<Package> search = _context
                .Packages
                .Where(p => p.FeedId == _feedScope.FeedId)
                .Where(p => p.Id.ToLower().Equals(packageId));
            search = AddSearchFilters(
                search,
                $"\"{request.PackageId}\"",
                request.IncludePrerelease,
                request.IncludeSemVer2,
                packageType: null,
                frameworkGroups: null);

            var results = await search
                .Select(p => p.NormalizedVersionString)
                .ToListAsync(cancellationToken);

            return new AutocompleteResponse
            {
                TotalHits = results.Count,
                Data = results,
                Context = AutocompleteContext.Default
            };
        }

        public async Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
        {
            // Optimize: Get package IDs first, then calculate download counts separately
            var dependentsQuery = _context
                .Packages
                .AsNoTracking()
                .Where(p => p.FeedId == _feedScope.FeedId)
                .Where(p => p.Listed)
                .Where(p => p.Dependencies.Any(d => d.Id == packageId));

            dependentsQuery = PackageOriginFilter.ApplyDiscoveryFilter(dependentsQuery, _searchOptions);

            var dependentPackageIds = await dependentsQuery
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Get download counts for dependent packages in a single query
            var downloadCounts = await PackageOriginFilter.ApplyDiscoveryFilter(
                    _context.Packages
                        .AsNoTracking()
                        .Where(p => p.FeedId == _feedScope.FeedId)
                        .Where(p => dependentPackageIds.Contains(p.Id)),
                    _searchOptions)
                .GroupBy(p => new { p.Id, p.Description })
                .Select(g => new
                {
                    g.Key.Id,
                    g.Key.Description,
                    TotalDownloads = g.Sum(p => (long)p.PackageDownloads.Count)
                })
                .OrderByDescending(x => x.TotalDownloads)
                .Take(20)
                .ToListAsync(cancellationToken);

            var results = downloadCounts
                .Select(r => new DependentResult
                {
                    Id = r.Id,
                    Description = r.Description,
                    TotalDownloads = r.TotalDownloads,
                })
                .ToList();

            return new DependentsResponse
            {
                TotalHits = results.Count,
                Data = results
            };
        }

        private async Task<int> SearchCountAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var frameworkGroups = FrameworkSearchFilter.GetMatchGroups(request, _frameworks);
            IQueryable<Package> search = _context.Packages.Where(p => p.FeedId == _feedScope.FeedId);

            search = AddSearchFilters(
                search,
                request.Query,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworkGroups,
                request.FrameworkFilterMode);

            return await search.Select(p => p.Id)
                .Distinct()
                .CountAsync();
        }

        private IQueryable<Package> AddSearchFilters(
            IQueryable<Package> query,
            string? searchQuery,
            bool includePrerelease,
            bool includeSemVer2,
            string? packageType,
            IReadOnlyList<IReadOnlyList<string>>? frameworkGroups,
            FrameworkFilterMode frameworkFilterMode = FrameworkFilterMode.Any)
        {
            searchQuery = searchQuery?.Trim();
            if (!includePrerelease)
            {
                query = query.Where(p => p.IsPrerelease == false);
            }

            if (!includeSemVer2)
            {
                query = query.Where(p => p.SemVerLevel != SemVerLevel.SemVer2);
            }

            if (!string.IsNullOrEmpty(packageType))
            {
                var normalizedType = packageType.Trim().ToLowerInvariant();
                query = query.Where(p => p.PackageTypes.Any(t => t.Name.ToLower() == normalizedType));
            }

            if (frameworkGroups is { Count: > 0 })
            {
                if (frameworkFilterMode == FrameworkFilterMode.All)
                {
                    foreach (var group in frameworkGroups)
                    {
                        var matches = group.ToArray();
                        query = query.Where(p => p.TargetFrameworks.Any(f => matches.Contains(f.Moniker)));
                    }
                }
                else
                {
                    var matches = frameworkGroups
                        .SelectMany(static group => group)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    query = query.Where(p => p.TargetFrameworks.Any(f => matches.Contains(f.Moniker)));
                }
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                var queries = searchQuery.Split([' ', '\t' ])
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(q => q.ToLower())
                    .ToArray();
                query = query.Where(p => queries.All(q => p.Id.ToLower().Contains(q)));
            }

            query = query.Where(p => p.Listed);

            return PackageOriginFilter.ApplyDiscoveryFilter(query, _searchOptions);
        }

        private string GetLicenseUrl(PackageSearchQueryResult pkg)
        {
            const string DeprecatedLicenseUrl = "https://aka.ms/deprecateLicenseUrl";
            
            // If the package has an embedded license and the URL is the deprecation URL,
            // return our own license download endpoint instead
            if (pkg.HasEmbeddedLicense && 
                (string.IsNullOrEmpty(pkg.LicenseUrl) || 
                 pkg.LicenseUrl.Equals(DeprecatedLicenseUrl, StringComparison.OrdinalIgnoreCase)))
            {
                return _url.GetPackageLicenseDownloadUrl(pkg.Id, pkg.Version);
            }

            return pkg.LicenseUrl;
        }
    }
}

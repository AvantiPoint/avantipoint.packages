using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    public class DatabaseSearchService : ISearchService
    {
        private readonly IContext _context;
        private readonly IFrameworkCompatibilityService _frameworks;
        private readonly IUrlGenerator _url;

        public DatabaseSearchService(IContext context, IFrameworkCompatibilityService frameworks, IUrlGenerator url)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
            _url = url ?? throw new ArgumentNullException(nameof(url));
        }

        public async Task<SearchResponse> SearchAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var count = await SearchCountAsync(request, cancellationToken);
            var frameworks = GetCompatibleFrameworksOrNull(request.Framework);
            IQueryable<Package> baseQuery = _context.Packages.AsNoTracking();

            // Apply search filters (e.g., query, prerelease, package type, frameworks)
            baseQuery = AddSearchFilters(
                baseQuery,
                request.Query,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            // Step 1: Get distinct package IDs and their total downloads in a single query
            // Use a subquery to calculate downloads once per package ID instead of per version
            var packageDownloads = await _context.Packages
                .AsNoTracking()
                .Where(p => baseQuery.Select(bp => bp.Id).Contains(p.Id))
                .GroupBy(p => p.Id)
                .Select(g => new
                {
                    PackageId = g.Key,
                    TotalDownloads = g.Sum(p => (long)p.PackageDownloads.Count)
                })
                .ToListAsync(cancellationToken);

            var downloadDict = packageDownloads.ToDictionary(x => x.PackageId, x => x.TotalDownloads);

            // Get distinct packages by Id, selecting the latest version with their types
            var latestPackagesQuery = await baseQuery
                .Include(p => p.PackageTypes)
                .GroupBy(p => p.Id)
                .Select(g => g.OrderByDescending(p => p.Published).FirstOrDefault())
                .OrderByDescending(p => downloadDict.ContainsKey(p.Id) ? downloadDict[p.Id] : 0)
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            // Get all package IDs we need versions for
            var packageIds = latestPackagesQuery.Select(p => p.Id).ToList();

            // Fetch all versions for all packages in a single query
            var allVersionsQuery = _context.Packages
                .AsNoTracking()
                .Where(p => packageIds.Contains(p.Id));

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
                    Published = new DateTimeOffset(pkg.Published, TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)),
                    Summary = pkg.Summary,
                    Tags = pkg.Tags ?? [],
                    Title = pkg.Title,
                    TotalDownloads = totalDownloads,
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
            IQueryable<Package> search = _context.Packages.AsNoTracking();

            if (!string.IsNullOrEmpty(request.Query))
            {
                var query = request.Query.ToLower();
                search = search.Where(p => p.Id.ToLower().Contains(query));
            }

            search = AddSearchFilters(
                search,
                request.Query,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks: null);

            // Optimize: Get package IDs first, then calculate download counts separately
            var packageIds = await search
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Get download counts for all matched packages in a single query
            var downloadCounts = await _context.Packages
                .AsNoTracking()
                .Where(p => packageIds.Contains(p.Id))
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
                .Where(p => p.Id.ToLower().Equals(packageId));
            search = AddSearchFilters(
                search,
                $"\"{request.PackageId}\"",
                request.IncludePrerelease,
                request.IncludeSemVer2,
                packageType: null,
                frameworks: null);

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
            var dependentPackageIds = await _context
                .Packages
                .AsNoTracking()
                .Where(p => p.Listed)
                .Where(p => p.Dependencies.Any(d => d.Id == packageId))
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Get download counts for dependent packages in a single query
            var downloadCounts = await _context
                .Packages
                .AsNoTracking()
                .Where(p => dependentPackageIds.Contains(p.Id))
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
            var frameworks = GetCompatibleFrameworksOrNull(request.Framework);
            IQueryable<Package> search = _context.Packages;

            search = AddSearchFilters(
                search,
                request.Query,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            return await search.Select(p => p.Id)
                .Distinct()
                .CountAsync();
        }

        private static IQueryable<Package> AddSearchFilters(
            IQueryable<Package> query,
            string searchQuery,
            bool includePrerelease,
            bool includeSemVer2,
            string packageType,
            IReadOnlyList<string> frameworks)
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
                query = query.Where(p => p.PackageTypes.Any(t => t.Name == packageType));
            }

            if (frameworks != null)
            {
                query = query.Where(p => p.TargetFrameworks.Any(f => frameworks.Contains(f.Moniker)));
            }

            if (!string.IsNullOrEmpty(searchQuery))
            {
                var queries = searchQuery.Split([' ', '\t' ])
                    .Where(x => !string.IsNullOrEmpty(x));
                query = query.Where(p => queries.All(q => p.Id.Contains(q)));
            }

            query = query.Where(p => p.Listed);

            return query;
        }

        private IReadOnlyList<string> GetCompatibleFrameworksOrNull(string framework)
        {
            if (framework == null) return null;

            return _frameworks.FindAllCompatibleFrameworks(framework);
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

    internal record PackageSearchQueryResult(string Id, NuGetVersion Version, string Description, string[] Authors, bool HasEmbeddedIcon, bool HasEmbeddedLicense, string IconUrl, string LicenseUrl, string ProjectUrl, DateTime Published, string Summary, string[] Tags, string Title, long TotalDownloads, List<SearchResultPackageType> PackageTypes);
    
    internal class VersionInfo
    {
        public NuGetVersion Version { get; set; }
        public int Downloads { get; set; }
    }
}

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

            // Step 1: Get distinct packages by Id, selecting the latest version
            var latestPackagesQuery = await baseQuery
                .Include(p => p.PackageTypes)
                .GroupBy(p => p.Id)
                .Select(g => new
                {
                    Latest = g.OrderByDescending(p => p.Published).FirstOrDefault(),
                    TotalDownloads = g.Sum(v => v.PackageDownloads.Count())
                })
                .OrderBy(p => p.TotalDownloads) 
                .Skip(request.Skip)
                .Take(request.Take)
                .ToListAsync(cancellationToken);

            var latestPackages = latestPackagesQuery
                .Select(x => new PackageSearchQueryResult(
                    x.Latest.Id,
                    x.Latest.Version,
                    x.Latest.Description,
                    x.Latest.Authors ?? [],
                    x.Latest.HasEmbeddedIcon,
                    x.Latest.IconUrlString,
                    x.Latest.LicenseUrlString,
                    x.Latest.ProjectUrlString,
                    x.Latest.Published,
                    x.Latest.Summary,
                    x.Latest.Tags ?? [],
                    x.Latest.Title,
                    x.TotalDownloads,
                    [.. x.Latest.PackageTypes.Select(pt => new SearchResultPackageType { Name = pt.Name })]
                ));

            // Step 2: Build SearchResult list and fetch versions separately
            var data = new List<SearchResult>();
            foreach (var pkg in latestPackages)
            {
                // Fetch versions for this package
                var versionsQuery = _context.Packages
                    .AsNoTracking()
                    .Where(p => p.Id == pkg.Id);

                if (!request.IncludePrerelease)
                {
                    versionsQuery = versionsQuery.Where(p => p.IsPrerelease == false);
                }

                var versions = await versionsQuery.Select(p => new
                {
                    p.Version,
                    Downloads = p.PackageDownloads.Count
                }).ToListAsync(cancellationToken);

                data.Add(new SearchResult
                {
                    PackageId = pkg.Id,
                    Version = pkg.Version.ToFullString(),
                    Description = pkg.Description,
                    Authors = pkg.Authors,
                    IconUrl = pkg.HasEmbeddedIcon
                        ? _url.GetPackageIconDownloadUrl(pkg.Id, pkg.Version)
                        : pkg.IconUrl,
                    LicenseUrl = pkg.LicenseUrl,
                    ProjectUrl = pkg.ProjectUrl,
                    RegistrationIndexUrl = _url.GetRegistrationIndexUrl(pkg.Id),
                    Published = new DateTimeOffset(pkg.Published, TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)),
                    Summary = pkg.Summary,
                    Tags = pkg.Tags,
                    Title = pkg.Title,
                    TotalDownloads = pkg.TotalDownloads,
                    PackageTypes = pkg.PackageTypes,
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
            IQueryable<Package> search = _context.Packages;

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

            var results = await search
                .OrderByDescending(p => p.PackageDownloads.Count)
                .Distinct()
                .Skip(request.Skip)
                .Take(request.Take)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            return new AutocompleteResponse
            {
                TotalHits = results.Count,
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
            var results = await _context
                .Packages
                .Where(p => p.Listed)
                .OrderByDescending(p => p.PackageDownloads.Count)
                .Where(p => p.Dependencies.Any(d => d.Id == packageId))
                .Take(20)
                .Select(r => new DependentResult
                {
                    Id = r.Id,
                    Description = r.Description,
                    TotalDownloads = r.PackageDownloads.Count,
                })
                .Distinct()
                .ToListAsync(cancellationToken);

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

        internal class DbSearchResult : SearchResult
        {
            public bool HasEmbeddedIcon { get; set; }
            public string IconUrlString { get; set; }
            public string LicenseUrlString { get; set; }
            public string ProjectUrlString { get; set; }
        }

        private static IQueryable<Package> AddSearchFilters(
            IQueryable<Package> query,
            string searchQuery,
            bool includePrerelease,
            bool includeSemVer2,
            string packageType,
            IReadOnlyList<string> frameworks)
        {
            query = FilterSearch(query, searchQuery);
            if (!includePrerelease)
            {
                query = query.Where(p => !p.IsPrerelease);
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

            query = query.Where(p => p.Listed);

            return query;
        }

        private static IQueryable<Package> FilterSearch(IQueryable<Package> query, string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery)) return query;

            var terms = searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().ToLower())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            if (terms.Count == 0) return query;

            // Combine terms with AND logic by applying each term's filter
            foreach (var term in terms)
            {
                // Handle pattern matching (e.g., "Package.*")
                if (term.EndsWith(".*"))
                {
                    var prefix = term[..^2]; // Remove ".*"
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        query = query.Where(x => x.Id.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase));
                    }
                }
                // Handle exact ID match for simple terms
                else if (!term.Contains('*') && !term.Contains('?'))
                {
                    query = query.Where(x => x.Id.Equals(term, StringComparison.CurrentCultureIgnoreCase));
                }
                // Handle wildcard patterns (e.g., "Pack*ge")
                else
                {
                    var pattern = term.Replace("*", "%").Replace("?", "_"); // Convert to SQL LIKE pattern
                    query = query.Where(x => EF.Functions.Like(x.Id.ToLower(), pattern));
                }

                // Tag search: Check if any tag contains the term (partial match)
                // Assuming Tags is a collection; if Tags is a delimited string, adjust accordingly
                query = query.Where(x => x.Tags.Any(t => t.Contains(term, StringComparison.CurrentCultureIgnoreCase)));
            }

            return query;
        }

        private IReadOnlyList<string> GetCompatibleFrameworksOrNull(string framework)
        {
            if (framework == null) return null;

            return _frameworks.FindAllCompatibleFrameworks(framework);
        }
    }

    internal record PackageSearchQueryResult(string Id, NuGetVersion Version, string Description, string[] Authors, bool HasEmbeddedIcon, string IconUrl, string LicenseUrl, string ProjectUrl, DateTime Published, string Summary, string[] Tags, string Title, long TotalDownloads, List<SearchResultPackageType> PackageTypes);
}

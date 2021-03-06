using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.EntityFrameworkCore;

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
            var result = new List<SearchResult>();
            var packages = await SearchImplAsync(
                request,
                cancellationToken);

            foreach (var package in packages)
            {
                var versions = package.OrderByDescending(p => p.Version).ToList();
                var latest = versions.First();
                var iconUrl = latest.HasEmbeddedIcon
                    ? _url.GetPackageIconDownloadUrl(latest.Id, latest.Version)
                    : latest.IconUrlString;

                result.Add(new SearchResult
                {
                    PackageId = latest.Id,
                    Version = latest.Version.ToFullString(),
                    Description = latest.Description,
                    Authors = latest.Authors,
                    IconUrl = iconUrl,
                    LicenseUrl = latest.LicenseUrlString,
                    ProjectUrl = latest.ProjectUrlString,
                    RegistrationIndexUrl = _url.GetRegistrationIndexUrl(latest.Id),
                    Summary = latest.Summary,
                    Tags = latest.Tags,
                    Title = latest.Title,
                    Published = new DateTimeOffset(latest.Published, TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)),
#pragma warning disable CS0618
                    TotalDownloads = versions.Sum(p => p.Downloads + p.PackageDownloads.Count),
#pragma warning restore CS0618
                    Versions = versions
                        .Select(p => new SearchResultVersion
                        {
                            RegistrationLeafUrl = _url.GetRegistrationLeafUrl(p.Id, p.Version),
                            Version = p.Version.ToFullString(),
#pragma warning disable CS0618
                            Downloads = p.PackageDownloads.Count + p.Downloads,
#pragma warning restore CS0618
                        })
                        .ToList()
                });
            }

            return new SearchResponse
            {
                TotalHits = await SearchCountAsync(request, cancellationToken),
                Data = result,
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
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks: null);

            var results = await search
#pragma warning disable CS0618
                .OrderByDescending(p => p.Downloads + p.PackageDownloads.Count)
#pragma warning restore CS0618
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
#pragma warning disable CS0618
                .OrderByDescending(p => p.Downloads + p.PackageDownloads.Count)
#pragma warning restore CS0618
                .Where(p => p.Dependencies.Any(d => d.Id == packageId))
                .Take(20)
                .Select(r => new DependentResult
                {
                    Id = r.Id,
                    Description = r.Description,
#pragma warning disable CS0618
                    TotalDownloads = r.Downloads + r.PackageDownloads.Count,
#pragma warning restore CS0618
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
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            if (!string.IsNullOrEmpty(request.Query))
            {
                var query = request.Query.ToLower();
                search = search.Where(p => p.Id.ToLower().Contains(query));
            }

            return await search.Select(p => p.Id)
                .Distinct()
                .CountAsync();
        }

        private async Task<List<IGrouping<string, Package>>> SearchImplAsync(
            SearchRequest request,
            CancellationToken cancellationToken)
        {
            var frameworks = GetCompatibleFrameworksOrNull(request.Framework);
            IQueryable<Package> search = _context.Packages
                .Include(x => x.PackageDownloads);

            search = AddSearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            if (!string.IsNullOrEmpty(request.Query))
            {
                var query = request.Query.ToLower();
                search = search.Where(p => p.Id.ToLower().Contains(query));
            }

            var packageIds = search
                .Include(x => x.PackageDownloads)
                .Select(p => p.Id)
                .Distinct()
                .OrderBy(id => id)
                .Skip(request.Skip)
                .Take(request.Take);

            // This query MUST fetch all versions for each package that matches the search,
            // otherwise the results for a package's latest version may be incorrect.
            // If possible, we'll find all these packages in a single query by matching
            // the package IDs in a subquery. Otherwise, run two queries:
            //   1. Find the package IDs that match the search
            //   2. Find all package versions for these package IDs
            if (_context.SupportsLimitInSubqueries)
            {
                search = _context.Packages.Include(x => x.PackageDownloads).Where(p => packageIds.Contains(p.Id));
            }
            else
            {
                var packageIdResults = await packageIds.ToListAsync(cancellationToken);

                search = _context.Packages.Include(x => x.PackageDownloads).Where(p => packageIdResults.Contains(p.Id));
            }

            search = AddSearchFilters(
                search,
                request.IncludePrerelease,
                request.IncludeSemVer2,
                request.PackageType,
                frameworks);

            var results = await search.ToListAsync(cancellationToken);

            return results.GroupBy(p => p.Id).ToList();
        }

        private IQueryable<Package> AddSearchFilters(
            IQueryable<Package> query,
            bool includePrerelease,
            bool includeSemVer2,
            string packageType,
            IReadOnlyList<string> frameworks)
        {
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

        private IReadOnlyList<string> GetCompatibleFrameworksOrNull(string framework)
        {
            if (framework == null) return null;

            return _frameworks.FindAllCompatibleFrameworks(framework);
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core;

public class PackageSearchDocumentFactory : IPackageSearchDocumentFactory
{
    private readonly IContext _context;
    private readonly SearchOptions _searchOptions;

    public PackageSearchDocumentFactory(IContext context, IOptions<SearchOptions> searchOptions)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _searchOptions = searchOptions?.Value ?? throw new ArgumentNullException(nameof(searchOptions));
    }

    public async Task<PackageSearchDocument?> CreateAsync(string packageId, CancellationToken cancellationToken)
    {
        var packages = await PackageOriginFilter.ApplyDiscoveryFilter(
                _context.Packages
                    .AsNoTracking()
                    .Include(p => p.PackageTypes)
                    .Include(p => p.TargetFrameworks)
                    .Include(p => p.Dependencies)
                    .Include(p => p.PackageDownloads)
                    .Where(p => p.Id == packageId && p.Listed),
                _searchOptions)
            .ToListAsync(cancellationToken);

        if (packages.Count == 0)
        {
            return null;
        }

        var ordered = packages.OrderByDescending(p => p.Version).ToList();
        var latestStable = ordered
            .FirstOrDefault(p => !p.IsPrerelease && p.SemVerLevel != SemVerLevel.SemVer2)
            ?? ordered[0];

        var totalDownloads = ordered.Sum(p => p.PackageDownloads?.Count ?? 0);
        var versions = ordered.Select(p => p.OriginalVersionString ?? p.NormalizedVersionString).ToArray();
        var versionDownloads = ordered.Select(p => (p.PackageDownloads?.Count ?? 0).ToString()).ToArray();
        var versionIsPrerelease = ordered.Select(p => p.IsPrerelease).ToArray();
        var versionIsSemVer2 = ordered.Select(p => p.SemVerLevel == SemVerLevel.SemVer2).ToArray();

        return new PackageSearchDocument
        {
            Key = packageId.ToLowerInvariant(),
            Id = packageId,
            Version = latestStable.OriginalVersionString ?? latestStable.NormalizedVersionString,
            Description = latestStable.Description,
            Authors = latestStable.Authors ?? [],
            HasEmbeddedIcon = latestStable.HasEmbeddedIcon,
            IconUrl = latestStable.IconUrlString,
            LicenseUrl = latestStable.LicenseUrlString,
            ProjectUrl = latestStable.ProjectUrlString,
            Published = latestStable.Published,
            Summary = latestStable.Summary,
            Tags = latestStable.Tags ?? [],
            Title = latestStable.Title,
            TotalDownloads = totalDownloads,
            Versions = versions,
            VersionDownloads = versionDownloads,
            VersionIsPrerelease = versionIsPrerelease,
            VersionIsSemVer2 = versionIsSemVer2,
            Dependencies = ordered
                .SelectMany(p => p.Dependencies ?? [])
                .Select(d => d.Id.ToLowerInvariant())
                .Distinct()
                .ToArray(),
            PackageTypes = ordered
                .SelectMany(p => p.PackageTypes ?? [])
                .Select(t => t.Name)
                .Distinct()
                .ToArray(),
            Frameworks = ordered
                .SelectMany(p => p.TargetFrameworks ?? [])
                .Select(f => f.Moniker)
                .Distinct()
                .ToArray(),
            VisibilityMask = SearchVisibility.ComputeMask(ordered),
            Origin = latestStable.Origin,
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Core;

public class PackageSearchDocumentFactory : IPackageSearchDocumentFactory
{
    private readonly IContext _context;

    public PackageSearchDocumentFactory(IContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PackageSearchDocument?> CreateAsync(string packageId, CancellationToken cancellationToken)
    {
        var packages = await _context.Packages
            .AsNoTracking()
            .Include(p => p.PackageTypes)
            .Include(p => p.TargetFrameworks)
            .Include(p => p.Dependencies)
            .Include(p => p.PackageDownloads)
            .Where(p => p.Id == packageId && p.Listed)
            .ToListAsync(cancellationToken);

        if (packages.Count == 0)
        {
            return null;
        }

        var ordered = packages.OrderByDescending(p => p.Version).ToList();
        var latest = ordered[0];

        var totalDownloads = ordered.Sum(p => p.PackageDownloads?.Count ?? 0);
        var versions = ordered.Select(p => p.OriginalVersionString ?? p.NormalizedVersionString).ToArray();
        var versionDownloads = ordered.Select(p => (p.PackageDownloads?.Count ?? 0).ToString()).ToArray();

        var filters = SearchDocumentFilters.Default;
        if (ordered.Any(p => p.IsPrerelease))
        {
            filters |= SearchDocumentFilters.IncludePrerelease;
        }

        if (ordered.Any(p => p.SemVerLevel == SemVerLevel.SemVer2))
        {
            filters |= SearchDocumentFilters.IncludeSemVer2;
        }

        return new PackageSearchDocument
        {
            Key = packageId.ToLowerInvariant(),
            Id = packageId,
            Version = latest.OriginalVersionString ?? latest.NormalizedVersionString,
            Description = latest.Description,
            Authors = latest.Authors ?? [],
            HasEmbeddedIcon = latest.HasEmbeddedIcon,
            IconUrl = latest.IconUrlString,
            LicenseUrl = latest.LicenseUrlString,
            ProjectUrl = latest.ProjectUrlString,
            Published = latest.Published,
            Summary = latest.Summary,
            Tags = latest.Tags ?? [],
            Title = latest.Title,
            TotalDownloads = totalDownloads,
            Versions = versions,
            VersionDownloads = versionDownloads,
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
            SearchFilters = filters.ToString(),
            Origin = latest.Origin,
        };
    }
}

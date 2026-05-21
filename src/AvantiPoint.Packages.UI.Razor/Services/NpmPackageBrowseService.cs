using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.UI.Services;

public sealed class NpmPackageBrowseService : INpmPackageBrowseService
{
    private readonly IContext _context;
    private readonly IFeedRegistry _feedRegistry;

    public NpmPackageBrowseService(IContext context, IFeedRegistry feedRegistry)
    {
        _context = context;
        _feedRegistry = feedRegistry;
    }

    public async Task<IReadOnlyList<NpmPackageListItem>> SearchAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var feedId = _feedRegistry.Feed.FeedId;
        var packages = _context.NpmPackages.AsNoTracking().Where(p => p.FeedId == feedId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            packages = packages.Where(p => EF.Functions.Like(p.Name, $"%{term}%"));
        }

        var rows = await packages
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .Select(p => new
            {
                p.Name,
                LatestVersion = p.Versions
                    .OrderByDescending(v => v.Published)
                    .Select(v => v.Version)
                    .FirstOrDefault(),
                Published = p.Versions
                    .OrderByDescending(v => v.Published)
                    .Select(v => (DateTime?)v.Published)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new NpmPackageListItem(r.Name, r.LatestVersion, r.Published))
            .ToList();
    }

    public async Task<NpmPackageDetailModel?> GetPackageAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        var feedId = _feedRegistry.Feed.FeedId;
        var normalizedName = NormalizePackageName(packageName);

        var package = await _context.NpmPackages
            .AsNoTracking()
            .Include(p => p.Versions)
            .Include(p => p.DistTags)
            .FirstOrDefaultAsync(
                p => p.FeedId == feedId && p.Name == normalizedName,
                cancellationToken);

        if (package is null)
        {
            return null;
        }

        var versions = package.Versions
            .OrderByDescending(v => v.Published)
            .Select(v => new NpmVersionListItem(v.Version, v.Published, v.Shasum))
            .ToList();

        var distTags = package.DistTags.ToDictionary(t => t.Tag, t => t.Version, StringComparer.OrdinalIgnoreCase);

        return new NpmPackageDetailModel(package.Name, versions, distTags);
    }

    private static string NormalizePackageName(string packageName) =>
        packageName.StartsWith('@')
            ? packageName
            : packageName.ToLowerInvariant();
}

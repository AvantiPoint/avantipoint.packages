using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.UI.Services;

public sealed class NpmPackageBrowseService : INpmPackageBrowseService
{
    private readonly IContext _context;
    private readonly IFeedRegistry _feedRegistry;
    private readonly IMirrorPolicyService _policy;
    private readonly NpmFeedOptions _npmOptions;

    public NpmPackageBrowseService(
        IContext context,
        IFeedRegistry feedRegistry,
        IMirrorPolicyService policy,
        IOptions<NpmFeedOptions> npmOptions)
    {
        _context = context;
        _feedRegistry = feedRegistry;
        _policy = policy;
        _npmOptions = npmOptions.Value;
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
            .Select(p => new
            {
                p.Name,
                Latest = p.Versions
                    .OrderByDescending(v => v.Published)
                    .Select(v => new { v.Version, v.Published, v.Origin })
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.Latest != null
                        && _policy.IncludeInDiscovery(FeedProtocol.Npm, r.Latest.Origin))
            .OrderBy(r => r.Name)
            .Skip(skip)
            .Take(take)
            .Select(r => new NpmPackageListItem(r.Name, r.Latest!.Version, r.Latest.Published))
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

using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
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
        skip = Math.Max(0, skip);
        take = Math.Clamp(take, 0, 100);
        if (take == 0)
        {
            return [];
        }

        var feedId = _feedRegistry.Feed.FeedId;
        var visibleOrigins = Enum.GetValues<PackageOrigin>()
            .Where(origin => _policy.IncludeInDiscovery(FeedProtocol.Npm, origin))
            .ToArray();
        var packages = _context.NpmPackages.AsNoTracking().Where(p => p.FeedId == feedId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLower();
            packages = packages.Where(p =>
                p.Name.ToLower().Contains(term)
                || p.Versions.Any(version =>
                    visibleOrigins.Contains(version.Origin)
                    && version.PackumentJson.ToLower().Contains(term)));
        }

        packages = packages.Where(p => p.Versions.Any(v => visibleOrigins.Contains(v.Origin)));

        var rows = await packages
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .Include(p => p.Versions)
            .Include(p => p.DistTags)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new { r.Name, Latest = SelectPreferredVisibleVersion(r.Versions, r.DistTags) })
            .Where(r => r.Latest != null)
            .Select(r => ToListItem(r.Name, r.Latest!))
            .ToList();
    }

    public async Task<NpmPackageDetailModel?> GetPackageAsync(
        string packageName,
        string? detailedVersion = null,
        CancellationToken cancellationToken = default)
    {
        var feedId = _feedRegistry.Feed.FeedId;
        var normalizedName = NormalizePackageName(packageName);

        var package = await _context.NpmPackages
            .AsNoTracking()
            .Include(p => p.DistTags)
            .FirstOrDefaultAsync(
                p => p.FeedId == feedId && p.Name == normalizedName,
                cancellationToken);

        if (package is null)
        {
            return null;
        }

        var visibleOrigins = Enum.GetValues<PackageOrigin>()
            .Where(origin => _policy.IncludeInDiscovery(FeedProtocol.Npm, origin))
            .ToArray();
        // PackumentJson can contain a README and large dependency maps. Project only the
        // lightweight fields needed by the version picker instead of materializing that JSON
        // once per version (popular npm packages can have several thousand versions).
        var visibleVersionEntities = await _context.NpmVersions
            .AsNoTracking()
            .Where(version => version.FeedId == feedId
                && version.PackageKey == package.Key
                && visibleOrigins.Contains(version.Origin))
            .OrderByDescending(version => version.Published)
            .Select(version => new NpmVersionSummary(
                version.Key,
                version.Version,
                version.Published,
                version.Shasum,
                version.Origin))
            .ToListAsync(cancellationToken);
        if (visibleVersionEntities.Count == 0)
        {
            return null;
        }

        var visibleVersions = visibleVersionEntities
            .Select(v => v.Version)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var distTags = package.DistTags
            .Where(t => visibleVersions.Contains(t.Version))
            .ToDictionary(t => t.Tag, t => t.Version, StringComparer.OrdinalIgnoreCase);

        var preferred = SelectPreferredVisibleVersion(visibleVersionEntities, package.DistTags)
            ?? visibleVersionEntities[0];
        var selected = visibleVersionEntities.FirstOrDefault(version =>
                string.Equals(version.Version, detailedVersion, StringComparison.Ordinal))
            ?? preferred;
        var detailedKeys = new[] { preferred.Key, selected.Key }.Distinct().ToArray();
        var detailedPackuments = await _context.NpmVersions
            .AsNoTracking()
            .Where(version => detailedKeys.Contains(version.Key))
            .Select(version => new { version.Key, version.PackumentJson })
            .ToListAsync(cancellationToken);
        var metadataByKey = detailedPackuments.ToDictionary(
            version => version.Key,
            version => NpmPackageMetadataReader.Read(version.PackumentJson));
        var packageMetadata = metadataByKey[preferred.Key];
        var versions = visibleVersionEntities
            .Select(version => metadataByKey.TryGetValue(version.Key, out var metadata)
                ? ToVersionListItem(version, metadata)
                : ToVersionListItem(version))
            .ToList();

        return new NpmPackageDetailModel(
            package.Name,
            versions,
            distTags,
            packageMetadata.Description,
            packageMetadata.Keywords,
            packageMetadata.Readme,
            packageMetadata.ReadmeFileName,
            packageMetadata.License,
            packageMetadata.HomepageUrl,
            packageMetadata.RepositoryUrl,
            packageMetadata.RepositoryDirectory,
            packageMetadata.BugsUrl,
            packageMetadata.BugsEmail,
            packageMetadata.Author,
            packageMetadata.Maintainers,
            packageMetadata.Funding);
    }

    private static NpmPackageListItem ToListItem(string name, NpmVersion latest)
    {
        var metadata = NpmPackageMetadataReader.Read(latest.PackumentJson);
        return new NpmPackageListItem(name, latest.Version, latest.Published, metadata.Description, metadata.Keywords);
    }

    private static NpmVersionListItem ToVersionListItem(
        NpmVersionSummary version,
        NpmPackageMetadata? metadata = null) =>
        new(
            version.Version,
            version.Published,
            version.Shasum,
            version.Origin,
            metadata?.Deprecated,
            metadata?.Integrity,
            metadata?.TarballFileName,
            metadata?.UnpackedSize,
            metadata?.FileCount,
            metadata?.Dependencies,
            metadata?.DevDependencies,
            metadata?.PeerDependencies,
            metadata?.OptionalDependencies,
            metadata?.OptionalPeerDependencies,
            metadata?.BundledDependencies,
            metadata?.Engines,
            metadata?.PackageManager,
            metadata?.ModuleType,
            metadata?.TypesEntryPoint,
            metadata?.OperatingSystems,
            metadata?.CpuArchitectures,
            metadata is null ? null : ToPresentationMetadata(metadata),
            metadata?.Publisher,
            metadata?.PublishingNpmVersion,
            metadata?.PublishingNodeVersion,
            metadata?.GitHead,
            metadata?.RegistrySignatureCount ?? 0,
            metadata?.Provenance);

    private static NpmPackagePresentationMetadata ToPresentationMetadata(NpmPackageMetadata metadata) =>
        new(
            metadata.Description,
            metadata.Keywords,
            metadata.Readme,
            metadata.ReadmeFileName,
            metadata.License,
            metadata.HomepageUrl,
            metadata.RepositoryUrl,
            metadata.RepositoryDirectory,
            metadata.BugsUrl,
            metadata.BugsEmail,
            metadata.Author,
            metadata.Maintainers,
            metadata.Funding);

    private static string NormalizePackageName(string packageName) =>
        packageName.StartsWith('@')
            ? packageName
            : packageName.ToLowerInvariant();

    private NpmVersion? SelectPreferredVisibleVersion(
        IEnumerable<NpmVersion> versions,
        IEnumerable<NpmDistTag> distTags)
    {
        var visibleVersions = versions
            .Where(v => _policy.IncludeInDiscovery(FeedProtocol.Npm, v.Origin))
            .ToList();
        var latestTag = distTags.FirstOrDefault(tag =>
            string.Equals(tag.Tag, "latest", StringComparison.OrdinalIgnoreCase));
        var taggedVersion = latestTag is null
            ? null
            : visibleVersions.FirstOrDefault(version =>
                string.Equals(version.Version, latestTag.Version, StringComparison.OrdinalIgnoreCase));

        return taggedVersion ?? visibleVersions.OrderByDescending(v => v.Published).FirstOrDefault();
    }

    private static NpmVersionSummary? SelectPreferredVisibleVersion(
        IReadOnlyList<NpmVersionSummary> versions,
        IEnumerable<NpmDistTag> distTags)
    {
        var latestTag = distTags.FirstOrDefault(tag =>
            string.Equals(tag.Tag, "latest", StringComparison.OrdinalIgnoreCase));
        var taggedVersion = latestTag is null
            ? null
            : versions.FirstOrDefault(version =>
                string.Equals(version.Version, latestTag.Version, StringComparison.OrdinalIgnoreCase));

        return taggedVersion ?? versions.OrderByDescending(version => version.Published).FirstOrDefault();
    }

    private sealed record NpmVersionSummary(
        int Key,
        string Version,
        DateTime Published,
        string? Shasum,
        PackageOrigin Origin);

}

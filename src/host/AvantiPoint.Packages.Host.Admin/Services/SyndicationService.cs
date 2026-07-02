using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Protocol;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class SyndicationService(
    IHostIdentityContext feedContext,
    IContext packageContext,
    IPackageStorageService packageStorageService,
    ISymbolStorageService symbolStorageService,
    IDownstreamPublishService downstreamPublishService) : ISyndicationService
{
    public async Task SyndicatePackageAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default)
    {
        foreach (var target in await TargetLookupAsync(packageId, cancellationToken))
        {
            await downstreamPublishService.PushPackageAsync(packageId, version, target, cancellationToken);
        }
    }

    public async Task SyndicateSymbolsAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default)
    {
        foreach (var target in await TargetLookupAsync(packageId, cancellationToken))
        {
            await downstreamPublishService.PushSymbolsAsync(packageId, version, target, cancellationToken);
        }
    }

    public async Task<SyndicationPushResult> PushToSourceAsync(string groupName, string targetName, CancellationToken cancellationToken = default)
    {
        var group = await feedContext.HostPackageGroups
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Name == groupName, cancellationToken);
        if (group is null)
        {
            throw new InvalidOperationException($"Package group '{groupName}' does not exist.");
        }

        var target = await feedContext.HostPublishTargets
            .FirstOrDefaultAsync(x => x.Name == targetName, cancellationToken);
        if (target is null)
        {
            throw new InvalidOperationException($"Publish target '{targetName}' does not exist.");
        }

        var pushed = new List<string>();
        var failed = new List<string>();

        foreach (var member in group.Members)
        {
            // Package.Version is a computed property (backed by OriginalVersionString /
            // NormalizedVersionString) and cannot be translated to SQL, so the candidates are
            // materialized first and ordered in memory - the same pattern used elsewhere in
            // the codebase (e.g. DefaultPackageMetadataService, PackageSearchDocumentFactory).
            var candidates = await packageContext.Packages
                .Where(x => x.Id == member.PackageId)
                .ToListAsync(cancellationToken);
            var package = candidates.OrderByDescending(x => x.Version).FirstOrDefault();

            if (package is null)
            {
                failed.Add(member.PackageId);
                continue;
            }

            var packagePushed = await downstreamPublishService.PushPackageAsync(package.Id, package.Version, target, cancellationToken);
            if (packagePushed)
            {
                // Symbols are best-effort: many packages have no snupkg, so a missing/failed
                // symbols push does not mark the package itself as failed.
                await downstreamPublishService.PushSymbolsAsync(package.Id, package.Version, target, cancellationToken);
                pushed.Add(member.PackageId);
            }
            else
            {
                failed.Add(member.PackageId);
            }
        }

        return new SyndicationPushResult(pushed, failed);
    }

    private async Task<IReadOnlyList<HostPublishTarget>> TargetLookupAsync(string packageId, CancellationToken cancellationToken)
    {
        var groups = await feedContext.HostPackageGroups
            .Include(x => x.Members)
            .Include(x => x.Syndications)
            .ThenInclude(x => x.PublishTarget)
            .Where(x => x.Members.Any(m => m.PackageId == packageId))
            .ToListAsync(cancellationToken);

        if (groups.Count == 0)
        {
            return [];
        }

        return groups.SelectMany(x => x.Syndications)
            .Select(x => x.PublishTarget)
            .DistinctBy(x => x.Name)
            .ToList();
    }
}

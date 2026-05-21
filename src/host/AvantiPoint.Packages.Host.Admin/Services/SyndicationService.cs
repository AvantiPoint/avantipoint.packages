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

    public async Task PushToSourceAsync(string groupName, string targetName, CancellationToken cancellationToken = default)
    {
        var group = await feedContext.HostPackageGroups
            .Include(x => x.Members)
            .FirstOrDefaultAsync(x => x.Name == groupName, cancellationToken);

        var target = await feedContext.HostPublishTargets
            .FirstOrDefaultAsync(x => x.Name == targetName, cancellationToken);

        if (group is null || target is null)
        {
            return;
        }

        foreach (var member in group.Members)
        {
            var package = await packageContext.Packages
                .Where(x => x.Id == member.PackageId)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            if (package is null)
            {
                continue;
            }

            await downstreamPublishService.PushPackageAsync(package.Id, package.Version, target, cancellationToken);
            await downstreamPublishService.PushSymbolsAsync(package.Id, package.Version, target, cancellationToken);
        }
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

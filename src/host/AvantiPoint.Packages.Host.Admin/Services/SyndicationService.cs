using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using AvantiPoint.Packages.Protocol;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class SyndicationService(
    IHostIdentityContext feedContext,
    IContext packageContext,
    IPackageStorageService packageStorageService,
    ISymbolStorageService symbolStorageService,
    IDownstreamPublishService downstreamPublishService,
    IEnumerable<IDownstreamPublisher> publishers,
    Events.IHostEventService eventService) : ISyndicationService
{
    public async Task SyndicatePackageAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default)
    {
        foreach (var target in await TargetLookupAsync(packageId, cancellationToken))
        {
            if (target.Protocol != PublishTargetProtocol.NuGet)
            {
                // Auto-syndication fires from the NuGet upload handler; a group with mixed
                // targets should only push to NuGet targets here. Cross-protocol promotion
                // (e.g. an npm/OCI target) is handled explicitly via PushToSourceAsync.
                continue;
            }

            await downstreamPublishService.PushPackageAsync(packageId, version, target, cancellationToken);
        }
    }

    public async Task SyndicateSymbolsAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default)
    {
        foreach (var target in await TargetLookupAsync(packageId, cancellationToken))
        {
            if (target.Protocol != PublishTargetProtocol.NuGet)
            {
                continue;
            }

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

        var publisher = publishers.FirstOrDefault(p => p.Protocol == target.Protocol);
        if (publisher is null)
        {
            throw new InvalidOperationException($"No downstream publisher is registered for protocol '{target.Protocol}'.");
        }

        var pushed = new List<string>();
        var failed = new List<string>();

        foreach (var member in group.Members)
        {
            var success = await publisher.PushAsync(member.PackageId, version: null, target, cancellationToken);
            (success ? pushed : failed).Add(member.PackageId);
        }

        await eventService.RecordAsync(
            "group.promoted",
            groupName,
            $"target={targetName}; pushed={pushed.Count}; failed={failed.Count}",
            cancellationToken);

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

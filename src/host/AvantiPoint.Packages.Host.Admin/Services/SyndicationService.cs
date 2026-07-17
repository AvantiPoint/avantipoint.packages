using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using AvantiPoint.Packages.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public sealed class SyndicationService(
    IHostIdentityContext feedContext,
    IDownstreamPublishService downstreamPublishService,
    IEnumerable<IDownstreamPublisher> publishers,
    Events.IHostEventService eventService,
    ILogger<SyndicationService> logger) : ISyndicationService
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

    public async Task SyndicateArtifactAsync(
        FeedArtifactEventContext context,
        CancellationToken cancellationToken = default)
    {
        var protocol = context.Surface.Protocol switch
        {
            FeedProtocol.Npm => PublishTargetProtocol.Npm,
            FeedProtocol.Oci => PublishTargetProtocol.Oci,
            _ => (PublishTargetProtocol?)null,
        };
        if (protocol is null)
        {
            return;
        }

        var publisher = publishers.FirstOrDefault(p => p.Protocol == protocol.Value);
        if (publisher is null)
        {
            logger.LogWarning(
                "Auto-syndication skipped {Artifact}: no publisher is registered for {Protocol}",
                context.ArtifactName,
                protocol.Value);
            return;
        }

        var request = new DownstreamPublishRequest(
            context.ArtifactName,
            context.Version,
            context.Surface);

        foreach (var target in (await TargetLookupAsync(context.ArtifactName, cancellationToken))
                     .Where(t => t.Protocol == protocol.Value))
        {
            try
            {
                if (!await publisher.PushAsync(request, target, cancellationToken))
                {
                    logger.LogWarning(
                        "Auto-syndication of {Artifact} {Version} to {Target} failed",
                        context.ArtifactName,
                        context.Version,
                        target.Name);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(
                    exception,
                    "Auto-syndication of {Artifact} {Version} to {Target} failed",
                    context.ArtifactName,
                    context.Version,
                    target.Name);
            }
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
            var success = await publisher.PushAsync(
                new DownstreamPublishRequest(member.PackageId),
                target,
                cancellationToken);
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

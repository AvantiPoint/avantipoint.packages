using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services.Publishers;

/// <summary>
/// Publishes NuGet packages (and symbols) via the existing <see cref="IDownstreamPublishService"/>.
/// </summary>
public sealed class NuGetDownstreamPublisher(
    IContext context,
    IDownstreamPublishService downstreamPublishService) : IDownstreamPublisher
{
    public PublishTargetProtocol Protocol => PublishTargetProtocol.NuGet;

    public async Task<bool> PushAsync(
        DownstreamPublishRequest request,
        HostPublishTarget target,
        CancellationToken cancellationToken = default)
    {
        var packageId = request.ArtifactName;
        NuGetVersion? nugetVersion = null;
        if (request.Version is not null)
        {
            nugetVersion = NuGetVersion.Parse(request.Version);
        }
        else
        {
            // Package.Version is a computed property (not a mapped column), so it can't be used in
            // an OrderBy translated to SQL - materialize the candidates first, then order in-memory.
            // Ordering by Published (instead of Version) would promote a backport pushed after a
            // newer release instead of the actual highest version.
            var packages = await context.Packages
                .AsNoTracking()
                .Where(p => p.Id == packageId)
                .Where(p => request.SourceSurface == null || p.FeedId == request.SourceSurface.FeedId)
                .ToListAsync(cancellationToken);
            nugetVersion = packages.OrderByDescending(p => p.Version).FirstOrDefault()?.Version;
        }

        if (nugetVersion is null)
        {
            return false;
        }

        var pushed = await downstreamPublishService.PushPackageAsync(packageId, nugetVersion, target, cancellationToken);
        if (pushed)
        {
            await downstreamPublishService.PushSymbolsAsync(packageId, nugetVersion, target, cancellationToken);
        }

        return pushed;
    }
}

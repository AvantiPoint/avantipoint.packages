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
        string packageId,
        string? version,
        HostPublishTarget target,
        CancellationToken cancellationToken = default)
    {
        NuGetVersion? nugetVersion = null;
        if (version is not null)
        {
            nugetVersion = NuGetVersion.Parse(version);
        }
        else
        {
            var package = await context.Packages
                .AsNoTracking()
                .Where(p => p.Id == packageId)
                .OrderByDescending(p => p.Published)
                .FirstOrDefaultAsync(cancellationToken);
            nugetVersion = package?.Version;
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

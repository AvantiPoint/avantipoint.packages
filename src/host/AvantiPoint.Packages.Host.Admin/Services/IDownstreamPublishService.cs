using AvantiPoint.Packages.Host.Admin.Entities;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public interface IDownstreamPublishService
{
    Task<bool> PushPackageAsync(
        string packageId,
        NuGetVersion version,
        HostPublishTarget target,
        CancellationToken cancellationToken = default);

    Task<bool> PushSymbolsAsync(
        string packageId,
        NuGetVersion version,
        HostPublishTarget target,
        CancellationToken cancellationToken = default);
}

using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Services;

public interface ISyndicationService
{
    Task SyndicatePackageAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default);

    Task SyndicateSymbolsAsync(string packageId, NuGetVersion version, CancellationToken cancellationToken = default);

    Task PushToSourceAsync(string groupName, string targetName, CancellationToken cancellationToken = default);
}

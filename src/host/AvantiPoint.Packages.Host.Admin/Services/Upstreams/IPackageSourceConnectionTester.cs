using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Host.Admin.Services.Upstreams;

public interface IPackageSourceConnectionTester
{
    Task<PackageSourceConnectionResult> TestAsync(
        PackageSource source,
        CancellationToken cancellationToken = default);
}

public sealed record PackageSourceConnectionResult(bool Succeeded, string Message);

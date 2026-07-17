#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Reads package archives from a NuGet global packages folder.
/// </summary>
public interface ILocalPackageCacheService
{
    /// <summary>
    /// Opens an existing package archive for asynchronous, read-only access.
    /// </summary>
    Task<LocalPackageCacheEntry?> TryOpenPackageAsync(
        string packageId,
        NuGetVersion version,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes an open package archive from the local NuGet cache.
/// </summary>
public sealed record LocalPackageCacheEntry(
    string Path,
    Stream Content,
    bool CopyToFeedStorage);

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol
{
    public interface IPublishClient
    {
        Task<bool> UploadPackageAsync(
            string packageId,
            NuGetVersion version,
            string apiKey,
            Stream packageStream,
            CancellationToken cancellationToken = default);

        Task<bool> UploadSymbolsPackageAsync(
            string packageId,
            NuGetVersion version,
            string apiKey,
            Stream packageStream,
            CancellationToken cancellationToken = default);
    }
}

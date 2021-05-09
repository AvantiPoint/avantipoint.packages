using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Protocol
{
    public interface IPublishClient
    {
        Task<bool> UploadPackageAsync(
            string packageId,
            string version,
            Uri packageSource,
            string apiKey,
            Stream packageStream,
            CancellationToken cancellationToken = default);

        Task<bool> UploadSymbolsPackageAsync(
            string packageId,
            string version,
            Uri packageSource,
            string apiKey,
            Stream packageStream,
            CancellationToken cancellationToken = default);
    }
}

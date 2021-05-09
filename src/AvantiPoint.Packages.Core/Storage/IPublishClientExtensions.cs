using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    public static class IPublishClientExtensions
    {
        public static async Task<bool> UploadPackageAsync(
            this IPublishClient publishClient,
            string packageId,
            string version,
            Uri packageSource,
            string apiKey,
            IPackageStorageService storageService,
            CancellationToken cancellationToken = default)
        {
            using var stream = await storageService.GetPackageStreamAsync(packageId, NuGetVersion.Parse(version), cancellationToken);
            if (stream == Stream.Null || cancellationToken.IsCancellationRequested)
                return false;

            return await publishClient.UploadPackageAsync(packageId, version, packageSource, apiKey, stream, cancellationToken);
        }

        public static async Task<bool> UploadSymbolsPackageAsync(
            this IPublishClient publishClient,
            string packageId,
            string version,
            Uri packageSource,
            string apiKey,
            ISymbolStorageService storageService,
            CancellationToken cancellationToken = default)
        {
            using var stream = await storageService.GetSymbolsAsync(packageId, version, cancellationToken);
            if (stream == Stream.Null || cancellationToken.IsCancellationRequested)
                return false;

            return await publishClient.UploadSymbolsPackageAsync(packageId, version, packageSource, apiKey, stream, cancellationToken);
        }
    }
}

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
        public static Task<bool> UploadPackageAsync(
            this NuGetClient client,
            string packageId,
            string version,
            IPackageStorageService storageService,
            CancellationToken cancellationToken = default)
        {
            return client.UploadPackageAsync(packageId, NuGetVersion.Parse(version), storageService, cancellationToken);
        }

        public static async Task<bool> UploadPackageAsync(
            this NuGetClient client,
            string packageId,
            NuGetVersion version,
            IPackageStorageService storageService,
            CancellationToken cancellationToken = default)
        {
            using var stream = await storageService.GetPackageStreamAsync(packageId, version, cancellationToken);
            if (stream == Stream.Null || cancellationToken.IsCancellationRequested)
                return false;

            return await client.UploadPackageAsync(packageId, version, stream, cancellationToken);
        }

        public static Task<bool> UploadSymbolsPackageAsync(
            this NuGetClient client,
            string packageId,
            string version,
            ISymbolStorageService storageService,
            CancellationToken cancellationToken = default)
        {
            return client.UploadSymbolsPackageAsync(packageId, NuGetVersion.Parse(version), storageService, cancellationToken);
        }

        public static async Task<bool> UploadSymbolsPackageAsync(
            this NuGetClient client,
            string packageId,
            NuGetVersion version,
            ISymbolStorageService storageService,
            CancellationToken cancellationToken = default)
        {
            using var stream = await storageService.GetSymbolsAsync(packageId, version, cancellationToken);
            if (stream == Stream.Null || cancellationToken.IsCancellationRequested)
                return false;

            return await client.UploadSymbolsPackageAsync(packageId, version, stream, cancellationToken);
        }
    }
}

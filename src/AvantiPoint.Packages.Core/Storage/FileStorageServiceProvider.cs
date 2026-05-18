using System;
using AvantiPoint.Packages.Core.Discovery;

namespace AvantiPoint.Packages.Core.Storage;

internal class FileStorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, FileStorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.FileSystem;
}

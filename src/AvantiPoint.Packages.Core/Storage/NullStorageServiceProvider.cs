using System;
using AvantiPoint.Packages.Core.Discovery;

namespace AvantiPoint.Packages.Core.Storage;

internal class NullStorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, NullStorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.Null;
}


using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace AvantiPoint.Packages.Gcp.Storage;

internal sealed class GoogleCloudStorageServiceProvider(IServiceProvider services)
    : GcsStorageServiceProvider(services)
{
    public override string Name => StorageProviderNames.GoogleCloudStorage;
}

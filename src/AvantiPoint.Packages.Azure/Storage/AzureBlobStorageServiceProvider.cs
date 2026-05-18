using System;
using AvantiPoint.Packages.Azure;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Azure.Storage;

internal class AzureBlobStorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, BlobStorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.AzureBlobStorage;

    public override void ValidateConfiguration()
    {
        var options = Services.GetRequiredService<IOptions<AzureBlobStorageOptions>>().Value;

        if (!string.IsNullOrWhiteSpace(options.ConnectionStringName))
        {
            options.ConnectionString = Services.GetRequiredService<IConfiguration>().GetConnectionString(options.ConnectionStringName);
        }

        var hasConnectionString = !string.IsNullOrWhiteSpace(options.ConnectionString);
        var hasAccountCredentials = !string.IsNullOrWhiteSpace(options.AccountName) &&
            !string.IsNullOrWhiteSpace(options.AccessKey);

        if (!hasConnectionString && !hasAccountCredentials)
        {
            throw new InvalidOperationException(
                "Azure Blob Storage requires either ConnectionString or AccountName + AccessKey to be configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Container))
        {
            throw new InvalidOperationException("Azure Blob Storage requires a container name.");
        }
    }
}

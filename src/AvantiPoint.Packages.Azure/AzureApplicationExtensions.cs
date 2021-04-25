using System;
using AvantiPoint.Packages.Azure;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    //using CloudStorageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount;
    //using StorageCredentials = Microsoft.WindowsAzure.Storage.Auth.StorageCredentials;
    using BlobServiceClient = global::Azure.Storage.Blobs.BlobServiceClient;
    using StorageSharedKeyCredential = global::Azure.Storage.StorageSharedKeyCredential;

    public static class AzureApplicationExtensions
    {
        public static NuGetApiOptions AddAzureBlobStorage(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<AzureBlobStorageOptions>(nameof(PackageFeedOptions.Storage));
            options.Services.AddTransient<BlobStorageService>();
            options.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<BlobStorageService>());

            options.Services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AzureBlobStorageOptions>>().Value;

                if (!string.IsNullOrEmpty(options.ConnectionString))
                {
                    return new BlobServiceClient(options.ConnectionString);
                }

                var credentials = new StorageSharedKeyCredential(options.AccountName, options.AccessKey);
                return new BlobServiceClient(new Uri($"https://{options.AccountName.ToLower()}.blob.core.windows.net"), credentials);
            });

            options.Services.AddTransient(provider =>
            {
                var options = provider.GetRequiredService<IOptionsSnapshot<AzureBlobStorageOptions>>().Value;
                var account = provider.GetRequiredService<BlobServiceClient>();
                return account.GetBlobContainerClient(options.Container);
            });

            options.Services.AddProvider<IStorageService>((provider, config) =>
            {
                if (!config.HasStorageType("AzureBlobStorage")) return null;

                return provider.GetRequiredService<BlobStorageService>();
            });

            return options;
        }

        public static NuGetApiOptions AddAzureBlobStorage(
            this NuGetApiOptions options,
            Action<AzureBlobStorageOptions> configure)
        {
            options.AddAzureBlobStorage();
            options.Services.Configure(configure);
            return options;
        }
    }
}

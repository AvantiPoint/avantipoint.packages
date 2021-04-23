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
        public static NuGetApiApplication AddAzureBlobStorage(this NuGetApiApplication app)
        {
            app.Services.AddNuGetApiOptions<AzureBlobStorageOptions>(nameof(PackageFeedOptions.Storage));
            app.Services.AddTransient<BlobStorageService>();
            app.Services.TryAddTransient<IStorageService>(provider => provider.GetRequiredService<BlobStorageService>());

            app.Services.AddSingleton(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AzureBlobStorageOptions>>().Value;

                if (!string.IsNullOrEmpty(options.ConnectionString))
                {
                    return new BlobServiceClient(options.ConnectionString);
                }

                var credentials = new StorageSharedKeyCredential(options.AccountName, options.AccessKey);
                return new BlobServiceClient(new Uri($"https://{options.AccountName.ToLower()}.blob.core.windows.net"), credentials);
            });

            app.Services.AddTransient(provider =>
            {
                var options = provider.GetRequiredService<IOptionsSnapshot<AzureBlobStorageOptions>>().Value;
                var account = provider.GetRequiredService<BlobServiceClient>();
                return account.GetBlobContainerClient(options.Container);
            });

            app.Services.AddProvider<IStorageService>((provider, config) =>
            {
                if (!config.HasStorageType("AzureBlobStorage")) return null;

                return provider.GetRequiredService<BlobStorageService>();
            });

            return app;
        }

        public static NuGetApiApplication AddAzureBlobStorage(
            this NuGetApiApplication app,
            Action<AzureBlobStorageOptions> configure)
        {
            app.AddAzureBlobStorage();
            app.Services.Configure(configure);
            return app;
        }
    }
}

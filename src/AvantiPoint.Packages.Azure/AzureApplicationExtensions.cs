using System;
using AvantiPoint.Packages.Azure;
using AvantiPoint.Packages.Azure.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    //using CloudStorageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount;
    //using StorageCredentials = Microsoft.WindowsAzure.Storage.Auth.StorageCredentials;
    using BlobServiceClient = global::Azure.Storage.Blobs.BlobServiceClient;
    using StorageSharedKeyCredential = global::Azure.Storage.StorageSharedKeyCredential;

    /// <summary>
    /// Extension methods for adding Azure Blob Storage support.
    /// Supports both auto-discovery (configuration-based) and explicit registration modes.
    /// </summary>
    public static class AzureApplicationExtensions
    {
        /// <summary>
        /// Adds Azure Blob Storage support for auto-discovery mode.
        /// The storage will be configured from <see cref="AzureBlobStorageOptions"/> in configuration.
        /// </summary>
        public static NuGetApiOptions AddAzureBlobStorage(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<AzureBlobStorageOptions>(nameof(PackageFeedOptions.Storage));
            options.Services.AddTransient<BlobStorageService>();

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

            options.Services.AddScoped<IStorageServiceProvider, AzureBlobStorageServiceProvider>();

            return options;
        }

        /// <summary>
        /// Adds Azure Blob Storage support for auto-discovery mode with custom options configuration.
        /// </summary>
        public static NuGetApiOptions AddAzureBlobStorage(
            this NuGetApiOptions options,
            Action<AzureBlobStorageOptions> configure)
        {
            options.AddAzureBlobStorage();
            options.Services.Configure(configure);
            return options;
        }

        /// <summary>
        /// Adds Azure Blob Storage support with explicit client registration.
        /// This allows full control over the BlobServiceClient configuration.
        /// The provider will use the explicitly registered client instead of creating one from options.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="blobServiceClient">The pre-configured BlobServiceClient instance.</param>
        /// <param name="containerName">The name of the blob container to use.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddAzureBlobStorage(
            this NuGetApiOptions options,
            BlobServiceClient blobServiceClient,
            string containerName)
        {
            // Register the client and container explicitly
            options.Services.AddSingleton(blobServiceClient);
            options.Services.AddTransient(provider => blobServiceClient.GetBlobContainerClient(containerName));

            // Register the storage service and provider
            options.Services.AddTransient<BlobStorageService>();
            options.Services.AddScoped<IStorageServiceProvider, AzureBlobStorageServiceProvider>();

            return options;
        }

        /// <summary>
        /// Adds Azure Blob Storage support with explicit client factory registration.
        /// This allows full control over how the BlobServiceClient and container are created.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="blobServiceClientFactory">Factory function to create the BlobServiceClient.</param>
        /// <param name="containerClientFactory">Factory function to create the BlobContainerClient.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddAzureBlobStorage(
            this NuGetApiOptions options,
            Func<IServiceProvider, BlobServiceClient> blobServiceClientFactory,
            Func<IServiceProvider, global::Azure.Storage.Blobs.BlobContainerClient> containerClientFactory)
        {
            // Register the client and container factories
            options.Services.AddSingleton(blobServiceClientFactory);
            options.Services.AddTransient(containerClientFactory);

            // Register the storage service and provider
            options.Services.AddTransient<BlobStorageService>();
            options.Services.AddScoped<IStorageServiceProvider, AzureBlobStorageServiceProvider>();

            return options;
        }

        /// <summary>
        /// Registers Azure Blob Storage provider for auto-discovery mode.
        /// The provider will be selected based on Storage:Type configuration.
        /// Does not register the storage service - it will be created on-demand by the provider.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverAzureBlobStorage(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<AzureBlobStorageOptions>(nameof(PackageFeedOptions.Storage));
            options.Services.AddTransient<BlobStorageService>();

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

            options.Services.AddScoped<IStorageServiceProvider, AzureBlobStorageServiceProvider>();

            return options;
        }
    }
}

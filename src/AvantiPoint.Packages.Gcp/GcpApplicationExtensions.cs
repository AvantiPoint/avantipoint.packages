using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Gcp;
using AvantiPoint.Packages.Gcp.Storage;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages;

public static class GcpApplicationExtensions
{
    public static NuGetApiOptions AddGcsStorage(this NuGetApiOptions options)
    {
        RegisterGcsStorage(options);
        return options;
    }

    public static NuGetApiOptions AddGcsStorage(this NuGetApiOptions options, Action<GcsStorageOptions> configure)
    {
        options.AddGcsStorage();
        options.Services.Configure(configure);
        return options;
    }

    public static NuGetApiOptions AddGcsStorage(this NuGetApiOptions options, StorageClient storageClient)
    {
        options.Services.AddSingleton(storageClient);
        options.Services.AddTransient<GcsStorageService>();
        options.Services.AddScoped<IStorageServiceProvider, GcsStorageServiceProvider>();
        options.Services.AddScoped<IStorageServiceProvider, GoogleCloudStorageServiceProvider>();
        return options;
    }

    public static NuGetApiOptions AutoDiscoverGcsStorage(this NuGetApiOptions options)
    {
        RegisterGcsStorage(options);
        return options;
    }

    private static void RegisterGcsStorage(NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<GcsStorageOptions>(nameof(PackageFeedOptions.Storage));
        options.Services.AddSingleton(provider =>
            GcsStorageClientFactory.Create(provider.GetRequiredService<IOptions<GcsStorageOptions>>()));
        options.Services.AddTransient<GcsStorageService>();
        options.Services.AddScoped<IStorageServiceProvider, GcsStorageServiceProvider>();
        options.Services.AddScoped<IStorageServiceProvider, GoogleCloudStorageServiceProvider>();
    }
}

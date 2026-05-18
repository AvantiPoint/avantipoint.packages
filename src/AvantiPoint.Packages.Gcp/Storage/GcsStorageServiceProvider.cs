using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Gcp.Storage;

internal class GcsStorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, GcsStorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.Gcs;

    public override void ValidateConfiguration()
    {
        var options = Services.GetRequiredService<IOptions<GcsStorageOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Bucket))
        {
            throw new InvalidOperationException("Google Cloud Storage requires a bucket name.");
        }

        if (!options.UseEmulator &&
            string.IsNullOrWhiteSpace(options.EmulatorHost) &&
            string.IsNullOrWhiteSpace(options.CredentialsPath) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
        {
            throw new InvalidOperationException(
                "Google Cloud Storage requires credentials (CredentialsPath, GOOGLE_APPLICATION_CREDENTIALS) or an emulator (EmulatorHost + UseEmulator).");
        }
    }
}

internal sealed class GoogleCloudStorageServiceProvider(IServiceProvider services)
    : GcsStorageServiceProvider(services)
{
    public override string Name => StorageProviderNames.GoogleCloudStorage;
}

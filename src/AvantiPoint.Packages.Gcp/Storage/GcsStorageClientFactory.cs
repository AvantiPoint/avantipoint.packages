using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Gcp.Storage;

internal static class GcsStorageClientFactory
{
    private static readonly object EmulatorLock = new();

    public static StorageClient Create(IOptions<GcsStorageOptions> options)
    {
        var value = options.Value;

        if (!string.IsNullOrWhiteSpace(value.EmulatorHost) && value.UseEmulator)
        {
            var emulatorUri = new Uri(NormalizeEmulatorHost(value.EmulatorHost));
            lock (EmulatorLock)
            {
                Environment.SetEnvironmentVariable("STORAGE_EMULATOR_HOST", null);
                return new StorageClientBuilder
                {
                    BaseUri = emulatorUri.GetLeftPart(UriPartial.Authority),
                    UnauthenticatedAccess = true,
                    EmulatorDetection = Google.Api.Gax.EmulatorDetection.None
                }.Build();
            }
        }

        var builder = new StorageClientBuilder();
        if (!string.IsNullOrWhiteSpace(value.CredentialsPath))
        {
            builder.CredentialsPath = value.CredentialsPath;
        }

        return builder.Build();
    }

    internal static string NormalizeEmulatorHost(string emulatorHost)
    {
        var host = emulatorHost.TrimEnd('/');
        return host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               host.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? host
            : "http://" + host;
    }
}

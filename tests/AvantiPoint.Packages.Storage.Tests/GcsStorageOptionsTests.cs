using AvantiPoint.Packages.Gcp;
using AvantiPoint.Packages.Gcp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

public class GcsStorageOptionsTests
{
    [Fact]
    public void ValidateConfiguration_ThrowsWhenBucketMissing()
    {
        var services = new ServiceCollection();
        services.Configure<GcsStorageOptions>(o =>
        {
            o.Bucket = string.Empty;
            o.EmulatorHost = "http://localhost:4443";
            o.UseEmulator = true;
        });
        services.AddSingleton<GcsStorageServiceProvider>();

        var provider = services.BuildServiceProvider();
        var storageProvider = provider.GetRequiredService<GcsStorageServiceProvider>();

        var ex = Assert.Throws<InvalidOperationException>(() => storageProvider.ValidateConfiguration());
        Assert.Contains("bucket", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

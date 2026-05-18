using AvantiPoint.Packages.Ftp;
using AvantiPoint.Packages.Ftp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

public class FtpStorageOptionsTests
{
    [Fact]
    public void ValidateConfiguration_ThrowsWhenPasswordMissing()
    {
        var services = new ServiceCollection();
        services.Configure<FtpStorageOptions>(o =>
        {
            o.Host = "localhost";
            o.Username = "user";
            o.Password = string.Empty;
        });
        services.AddSingleton<FtpStorageServiceProvider>();

        var provider = services.BuildServiceProvider();
        var storageProvider = provider.GetRequiredService<FtpStorageServiceProvider>();

        var ex = Assert.Throws<InvalidOperationException>(() => storageProvider.ValidateConfiguration());
        Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

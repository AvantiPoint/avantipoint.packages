using AvantiPoint.Packages.Sftp;
using AvantiPoint.Packages.Sftp.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Storage.Tests;

public class SftpStorageOptionsTests
{
    [Fact]
    public void ValidateConfiguration_ThrowsWhenCredentialsMissing()
    {
        var services = new ServiceCollection();
        services.Configure<SftpStorageOptions>(o =>
        {
            o.Host = "localhost";
            o.Username = "user";
        });
        services.AddSingleton<SftpStorageServiceProvider>();

        var provider = services.BuildServiceProvider();
        var storageProvider = provider.GetRequiredService<SftpStorageServiceProvider>();

        var ex = Assert.Throws<InvalidOperationException>(() => storageProvider.ValidateConfiguration());
        Assert.Contains("password", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Ftp.Storage;

internal class FtpStorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, FtpStorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.Ftp;

    public override void ValidateConfiguration()
    {
        var options = Services.GetRequiredService<IOptions<FtpStorageOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException("FTP storage requires a host.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new InvalidOperationException("FTP storage requires a username.");
        }

        if (string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException("FTP storage requires a password.");
        }
    }
}

using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Sftp.Storage;

internal class SftpStorageServiceProvider(IServiceProvider services)
    : ServiceDiscoveryProvider<IStorageService, SftpStorageService>(services), IStorageServiceProvider
{
    public override string Name => StorageProviderNames.Sftp;

    public override void ValidateConfiguration()
    {
        var options = Services.GetRequiredService<IOptions<SftpStorageOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException("SFTP storage requires a host.");
        }

        if (string.IsNullOrWhiteSpace(options.Username))
        {
            throw new InvalidOperationException("SFTP storage requires a username.");
        }

        var hasPassword = !string.IsNullOrWhiteSpace(options.Password);
        var hasKey = !string.IsNullOrWhiteSpace(options.PrivateKeyPath);

        if (!hasPassword && !hasKey)
        {
            throw new InvalidOperationException("SFTP storage requires a password or private key path.");
        }
    }
}

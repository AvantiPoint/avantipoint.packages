using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Sftp;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Sftp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages;

public static class SftpApplicationExtensions
{
    public static NuGetApiOptions AddSftpStorage(this NuGetApiOptions options)
    {
        RegisterSftpStorage(options);
        return options;
    }

    public static NuGetApiOptions AddSftpStorage(this NuGetApiOptions options, Action<SftpStorageOptions> configure)
    {
        options.AddSftpStorage();
        options.Services.Configure(configure);
        return options;
    }

    public static NuGetApiOptions AutoDiscoverSftpStorage(this NuGetApiOptions options)
    {
        RegisterSftpStorage(options);
        return options;
    }

    private static void RegisterSftpStorage(NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<SftpStorageOptions>(nameof(PackageFeedOptions.Storage));
        // Runs after the named connection string is resolved into ConnectionString.
        options.Services.PostConfigure<SftpStorageOptions>(o => o.ApplyConnectionString());
        options.Services.AddTransient<SftpStorageService>();
        options.Services.AddScoped<IStorageServiceProvider, SftpStorageServiceProvider>();
    }
}

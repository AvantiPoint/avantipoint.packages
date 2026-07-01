using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Ftp;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Ftp.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages;

public static class FtpApplicationExtensions
{
    public static NuGetApiOptions AddFtpStorage(this NuGetApiOptions options)
    {
        RegisterFtpStorage(options);
        return options;
    }

    public static NuGetApiOptions AddFtpStorage(this NuGetApiOptions options, Action<FtpStorageOptions> configure)
    {
        options.AddFtpStorage();
        options.Services.Configure(configure);
        return options;
    }

    public static NuGetApiOptions AutoDiscoverFtpStorage(this NuGetApiOptions options)
    {
        RegisterFtpStorage(options);
        return options;
    }

    private static void RegisterFtpStorage(NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<FtpStorageOptions>(nameof(PackageFeedOptions.Storage));
        // Runs after the named connection string is resolved into ConnectionString.
        options.Services.PostConfigure<FtpStorageOptions>(o => o.ApplyConnectionString());
        options.Services.AddTransient<FtpStorageService>();
        options.Services.AddScoped<IStorageServiceProvider, FtpStorageServiceProvider>();
    }
}

using System.IO;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostAdminServiceExtensions
{
    public static IServiceCollection AddHostAdminServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HostSettings>(configuration.GetSection("Host"));
        services.Configure<HostAccessOptions>(configuration.GetSection("Host:Access"));
        services.Configure<HostAuthenticationOptions>(configuration.GetSection("Host:Authentication"));

        services.AddHttpContextAccessor();

        // Encrypt stored feed credentials (upstream source secrets, downstream publish tokens)
        // at rest. Registered before AddNuGetPackageApi so Core's fallback NullSecretProtector
        // is never used in the Host.
        //
        // The key ring MUST be persisted to a durable location shared across restarts/instances,
        // or previously-encrypted secrets become unreadable the moment the key ring is lost (for
        // example a container recreated with only /data mounted). Configure
        // Host:DataProtection:KeyPath to point at the same durable volume as the database/storage;
        // appsettings.Docker.json sets this to /data/dataprotection-keys, alongside /data/packages.db.
        var dataProtection = services.AddDataProtection()
            .SetApplicationName(configuration["Host:DataProtection:ApplicationName"] ?? "AvantiPoint.Packages.Host");

        var keyPath = configuration["Host:DataProtection:KeyPath"];
        if (!string.IsNullOrWhiteSpace(keyPath))
        {
            Directory.CreateDirectory(keyPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
        }

        services.TryAddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        services.AddSingleton<IHostTokenHasher, HostTokenHasher>();
        services.AddScoped<IPackageAuthenticationService, DatabasePackageAuthenticationService>();
        services.AddScoped<IHostUserProvisioner, HostUserProvisioner>();
        services.AddHttpClient(nameof(HostExternalLoginValidator));
        services.AddScoped<IHostExternalLoginValidator, HostExternalLoginValidator>();
        services.AddScoped<INuGetFeedActionHandler, HostNuGetFeedActionHandler>();
        services.AddScoped<ISyndicationService, SyndicationService>();
        services.AddScoped<IDownstreamPublishService, DownstreamPublishService>();

        services.AddHostEmailServices(configuration);
        services.AddHostedService<TokenExpirationNotificationService>();

        return services;
    }
}

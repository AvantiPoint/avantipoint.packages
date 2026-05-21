using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostAdminServiceExtensions
{
    public static IServiceCollection AddHostAdminServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HostSettings>(configuration.GetSection("Host"));
        services.Configure<HostAccessOptions>(configuration.GetSection("Host:Access"));
        services.Configure<HostAuthenticationOptions>(configuration.GetSection("Host:Authentication"));

        services.AddHttpContextAccessor();
        services.AddSingleton<IHostTokenHasher, HostTokenHasher>();
        services.AddScoped<IPackageAuthenticationService, DatabasePackageAuthenticationService>();
        services.AddScoped<IHostUserProvisioner, HostUserProvisioner>();
        services.AddScoped<INuGetFeedActionHandler, HostNuGetFeedActionHandler>();
        services.AddScoped<ISyndicationService, SyndicationService>();
        services.AddScoped<IDownstreamPublishService, DownstreamPublishService>();

        services.AddHostEmailServices(configuration);
        services.AddHostedService<TokenExpirationNotificationService>();

        return services;
    }
}

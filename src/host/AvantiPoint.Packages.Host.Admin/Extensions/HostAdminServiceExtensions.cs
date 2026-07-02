using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Secrets;
using AvantiPoint.Packages.Host.Admin.Services.Tokens;
using AvantiPoint.Packages.Hosting;
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
        services.AddDataProtection();
        services.TryAddSingleton<ISecretProtector, DataProtectionSecretProtector>();

        services.AddSingleton<IHostTokenHasher, HostTokenHasher>();
        services.AddScoped<IPackageAuthenticationService, DatabasePackageAuthenticationService>();
        services.AddScoped<IHostUserProvisioner, HostUserProvisioner>();
        services.AddHttpClient(nameof(HostExternalLoginValidator));
        services.AddScoped<IHostExternalLoginValidator, HostExternalLoginValidator>();
        services.AddScoped<INuGetFeedActionHandler, HostNuGetFeedActionHandler>();
        services.AddScoped<ISyndicationService, SyndicationService>();
        services.AddScoped<IDownstreamPublishService, DownstreamPublishService>();
        services.AddHttpClient(nameof(Services.Publishers.NpmDownstreamPublisher));
        services.AddScoped<Services.Publishers.IDownstreamPublisher, Services.Publishers.NuGetDownstreamPublisher>();
        services.AddScoped<Services.Publishers.IDownstreamPublisher, Services.Publishers.NpmDownstreamPublisher>();

        services.AddHostEmailServices(configuration);
        services.AddHostedService<TokenExpirationNotificationService>();

        return services;
    }
}

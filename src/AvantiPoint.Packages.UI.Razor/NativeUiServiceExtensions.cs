using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.UI;

public static class NativeUiServiceExtensions
{
    public static IServiceCollection AddNativePackageBrowseUi(this IServiceCollection services, string? authenticatedReadRole = null)
    {
        services.AddHttpContextAccessor();
        services.Configure<NativePackageBrowseOptions>(options => options.AuthenticatedReadRole = authenticatedReadRole);
        services.TryAddScoped<NativePackageBrowseService>();
        return services;
    }
}

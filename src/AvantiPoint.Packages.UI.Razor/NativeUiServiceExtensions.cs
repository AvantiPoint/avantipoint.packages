using AvantiPoint.Packages.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.UI;

public static class NativeUiServiceExtensions
{
    public static IServiceCollection AddNativePackageBrowseUi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<NativePackageBrowseService>();
        return services;
    }
}

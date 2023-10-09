#if !NET6_0
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Hosting.Internals;

internal static class PackageActionFilterExtensions
{
    internal static RouteHandlerBuilder AddPackageAction<T>(this RouteHandlerBuilder builder, WebApplication app)
        where T : PackageActionFilter
    {
        using var sp = app.Services.CreateScope();
        var handler = sp.ServiceProvider.GetService<INuGetFeedActionHandler>();
        if(handler is not null)
        {
            return builder.AddEndpointFilter<T>();
        }

        return builder;
    }

}
#endif
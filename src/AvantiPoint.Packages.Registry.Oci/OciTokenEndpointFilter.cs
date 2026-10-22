using AvantiPoint.Feed.Platform;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci;

internal sealed class OciTokenEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var accessor = context.HttpContext.RequestServices.GetRequiredService<ISurfaceContextAccessor>();
        if (accessor.Current is null || accessor.Current.Protocol != FeedProtocol.Oci) return Results.NotFound();
        return await next(context);
    }
}

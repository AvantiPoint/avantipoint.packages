using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Feed.Platform;

public interface IPublicBaseUrlProvider
{
    Uri GetRequestOrigin(HttpContext httpContext);

    Uri GetSurfacePublicBaseUrl(HttpContext httpContext, string routePrefix);
}

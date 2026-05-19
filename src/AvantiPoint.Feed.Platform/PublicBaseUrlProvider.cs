using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Feed.Platform;

public sealed class PublicBaseUrlProvider : IPublicBaseUrlProvider
{
    public Uri GetRequestOrigin(HttpContext httpContext)
    {
        var request = httpContext.Request;
        var builder = new UriBuilder
        {
            Scheme = request.Scheme,
            Host = request.Host.Host,
            Port = request.Host.Port ?? -1,
            Path = request.PathBase.Value?.TrimEnd('/') ?? string.Empty,
        };

        if (builder.Port == -1
            || (builder.Scheme == "https" && builder.Port == 443)
            || (builder.Scheme == "http" && builder.Port == 80))
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }

    public Uri GetSurfacePublicBaseUrl(HttpContext httpContext, string routePrefix)
    {
        var origin = GetRequestOrigin(httpContext);
        if (string.IsNullOrEmpty(routePrefix) || routePrefix == "/")
        {
            return origin;
        }

        var prefix = routePrefix.Trim('/');
        var basePath = origin.AbsolutePath.TrimEnd('/');
        var combinedPath = string.IsNullOrEmpty(basePath)
            ? $"/{prefix}"
            : $"{basePath}/{prefix}";

        return new UriBuilder(origin.Scheme, origin.Host, origin.Port, combinedPath + "/").Uri;
    }
}

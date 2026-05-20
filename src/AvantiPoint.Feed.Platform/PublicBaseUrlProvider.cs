using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Feed.Platform;

public sealed class PublicBaseUrlProvider : IPublicBaseUrlProvider
{
    private readonly IOptionsMonitor<FeedOptions> _feedOptions;
    private readonly IOptionsMonitor<PackageFeedOptions> _packageFeedOptions;

    public PublicBaseUrlProvider(
        IOptionsMonitor<FeedOptions> feedOptions,
        IOptionsMonitor<PackageFeedOptions> packageFeedOptions)
    {
        _feedOptions = feedOptions;
        _packageFeedOptions = packageFeedOptions;
    }

    public Uri GetRequestOrigin(HttpContext httpContext)
    {
        var configured = _feedOptions.CurrentValue.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured)
            && Uri.TryCreate(configured.TrimEnd('/') + "/", UriKind.Absolute, out var configuredUri))
        {
            return configuredUri;
        }

        var request = httpContext.Request;
        var scheme = GetForwardedHeader(request, "X-Forwarded-Proto") ?? request.Scheme;
        var host = GetForwardedHeader(request, "X-Forwarded-Host") ?? request.Host.Value;
        var pathBase = GetForwardedHeader(request, "X-Forwarded-Prefix")
            ?? request.PathBase.Value
            ?? _packageFeedOptions.CurrentValue.PathBase
            ?? string.Empty;

        return BuildOriginUri(scheme, host, pathBase);
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

    private static string GetForwardedHeader(HttpRequest request, string headerName)
    {
        if (!request.Headers.TryGetValue(headerName, out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Uri BuildOriginUri(string scheme, string hostValue, string pathBase)
    {
        var host = HostString.FromUriComponent(hostValue);
        var builder = new UriBuilder
        {
            Scheme = scheme,
            Host = host.Host,
            Port = host.Port ?? -1,
            Path = pathBase.TrimEnd('/'),
        };

        if (builder.Port == -1
            || (builder.Scheme == "https" && builder.Port == 443)
            || (builder.Scheme == "http" && builder.Port == 80))
        {
            builder.Port = -1;
        }

        return builder.Uri;
    }
}

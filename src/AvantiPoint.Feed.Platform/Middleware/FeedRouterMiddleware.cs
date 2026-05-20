using AvantiPoint.Feed.Platform.Routing;
using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Feed.Platform.Middleware;

/// <summary>
/// Resolves feed surface context and rewrites OCI paths. Register via
/// <see cref="Extensions.FeedServiceCollectionExtensions.UseAvantiPointFeedPlatform"/>
/// before <c>UseRouting()</c> so path rewrites affect endpoint matching.
/// </summary>
public sealed class FeedRouterMiddleware
{
    private readonly RequestDelegate _next;

    public FeedRouterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IFeedRegistry registry,
        ISurfaceContextAccessor surfaceAccessor,
        IPublicBaseUrlProvider baseUrlProvider)
    {
        var path = context.Request.Path;
        var match = FeedSurfaceMatcher.Match(registry, path);

        if (match is null)
        {
            if (IsUnregisteredFeedPath(path, registry))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await _next(context);
            return;
        }

        var registration = match.Registration;
        var publicBaseUrl = baseUrlProvider.GetSurfacePublicBaseUrl(context, registration.RoutePrefix);

        surfaceAccessor.Current = new SurfaceContext(
            registry.Feed.FeedId,
            registration.Protocol,
            registration.SurfaceId,
            registration.OciSegment,
            registration.RoutePrefix,
            publicBaseUrl);

        if (match.StripSegmentPrefix && !string.IsNullOrEmpty(registration.OciSegment))
        {
            var pathValue = path.Value ?? string.Empty;
            var segmentPrefix = $"/{registration.OciSegment}";
            if (pathValue.StartsWith(segmentPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = pathValue[segmentPrefix.Length..];
                if (string.IsNullOrEmpty(remainder))
                {
                    remainder = "/";
                }

                context.Request.Path = new PathString(remainder);
            }
        }

        await _next(context);
    }

    private static bool IsUnregisteredFeedPath(PathString path, IFeedRegistry registry)
    {
        var value = NormalizePath(path);

        if ((StartsWithSegment(value, "/v2/") || value.Equals("/v2", StringComparison.OrdinalIgnoreCase))
            && registry.TryGetDefaultOciSurface() is null)
        {
            return true;
        }

        if (registry.TryGetNpmSurface() is null
            && (StartsWithSegment(value, "/npm/") || value.Equals("/npm", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (TryGetNamedOciSegment(value, out var segment)
            && registry.TryGetOciSurfaceBySegment(segment) is null)
        {
            return true;
        }

        return false;
    }

    private static bool TryGetNamedOciSegment(string path, out string segment)
    {
        segment = null;
        var parts = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2
            && parts[1].Equals("v2", StringComparison.OrdinalIgnoreCase)
            && !parts[0].Equals("v3", StringComparison.OrdinalIgnoreCase))
        {
            segment = parts[0];
            return true;
        }

        return false;
    }

    private static string NormalizePath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith('/') ? value : "/" + value;
    }

    private static bool StartsWithSegment(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}

using Microsoft.AspNetCore.Http;

namespace AvantiPoint.Feed.Platform.Routing;

public static class FeedSurfaceMatcher
{
    public static SurfaceMatchResult? Match(IFeedRegistry registry, PathString path)
    {
        var value = path.Value ?? "/";
        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (StartsWithSegment(value, "/v3/")
            || StartsWithSegment(value, "/api/")
            || StartsWithSegment(value, "/shield/"))
        {
            var nuget = registry.TryGetNuGetSurface();
            return nuget is null ? null : new SurfaceMatchResult(nuget, value, StripSegmentPrefix: false);
        }

        if (value.Equals("/token", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/token?", StringComparison.OrdinalIgnoreCase))
        {
            var defaultOci = registry.TryGetDefaultOciSurface();
            return defaultOci is null ? null : new SurfaceMatchResult(defaultOci, value, StripSegmentPrefix: false);
        }

        foreach (var surface in registry.Surfaces.Where(s => s.Protocol == FeedProtocol.Oci && !string.IsNullOrEmpty(s.OciSegment)))
        {
            var routePrefix = surface.RoutePrefix.TrimEnd('/');
            if (!routePrefix.StartsWith('/'))
            {
                routePrefix = "/" + routePrefix;
            }

            if (value.Equals($"{routePrefix}/token", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith($"{routePrefix}/token?", StringComparison.OrdinalIgnoreCase))
            {
                return new SurfaceMatchResult(surface, value, StripSegmentPrefix: false);
            }
        }

        foreach (var surface in registry.Surfaces.Where(s =>
                     s.Protocol == FeedProtocol.Oci
                     && !string.IsNullOrEmpty(s.OciSegment)
                     && s.AllowV2EmbeddedSegmentRouting))
        {
            var embeddedPrefix = $"/v2/{surface.OciSegment}";
            if (value.StartsWith(embeddedPrefix + "/", StringComparison.OrdinalIgnoreCase)
                || value.Equals(embeddedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new SurfaceMatchResult(surface, value, StripSegmentPrefix: false, StripV2EmbeddedSegment: true);
            }
        }

        if (StartsWithSegment(value, "/v2/") || value.Equals("/v2", StringComparison.OrdinalIgnoreCase))
        {
            var oci = registry.TryGetDefaultOciSurface();
            return oci is null ? null : new SurfaceMatchResult(oci, value, StripSegmentPrefix: false);
        }

        foreach (var surface in registry.Surfaces.Where(s => s.Protocol == FeedProtocol.Oci && !string.IsNullOrEmpty(s.OciSegment)))
        {
            var prefix = $"/{surface.OciSegment}/v2";
            if (value.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                || value.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return new SurfaceMatchResult(surface, value, StripSegmentPrefix: true);
            }
        }

        foreach (var surface in registry.Surfaces.Where(s => s.Protocol == FeedProtocol.Npm))
        {
            var prefix = surface.RoutePrefix.TrimEnd('/');
            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "/npm";
            }

            if (!prefix.StartsWith('/'))
            {
                prefix = "/" + prefix;
            }

            if (value.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)
                || value.Equals(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return new SurfaceMatchResult(surface, value, StripSegmentPrefix: false);
            }
        }

        return null;
    }

    private static bool StartsWithSegment(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}

using AvantiPoint.Feed.Platform.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Feed.Platform.Health;

public static class FeedHealthExtensions
{
    public static IEndpointRouteBuilder MapFeedHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/feeds", (IFeedRegistry registry, FeedMetricsService metrics) =>
        {
            var surfaces = registry.Surfaces.Select(s => new
            {
                s.SurfaceId,
                Protocol = s.Protocol.ToString(),
                s.OciSegment,
                s.RoutePrefix,
                Status = "ready",
            });

            return Results.Json(new
            {
                feedId = registry.Feed.FeedId,
                surfaces,
                metrics = new
                {
                    push = metrics.GetPushCounts(),
                    pull = metrics.GetPullCounts(),
                },
            });
        });

        return endpoints;
    }
}

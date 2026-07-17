using AvantiPoint.Feed.Platform.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AvantiPoint.Feed.Platform.Health;

public static class FeedHealthExtensions
{
    public static IEndpointRouteBuilder MapFeedHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/feeds", GetFeedHealthAsync);
        endpoints.MapGet("/health/feeds/{name}", GetNamedFeedHealthAsync);

        return endpoints;
    }

    private static Task<IResult> GetFeedHealthAsync(
        IFeedRegistry registry,
        FeedMetricsService metrics,
        HealthCheckService healthChecks,
        CancellationToken cancellationToken) =>
        BuildResponseAsync(registry, metrics, healthChecks, cancellationToken);

    private static Task<IResult> GetNamedFeedHealthAsync(
        string name,
        IFeedRegistry registry,
        FeedMetricsService metrics,
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(name, registry.Feed.FeedId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IResult>(Results.NotFound());
        }

        return BuildResponseAsync(registry, metrics, healthChecks, cancellationToken);
    }

    private static async Task<IResult> BuildResponseAsync(
        IFeedRegistry registry,
        FeedMetricsService metrics,
        HealthCheckService healthChecks,
        CancellationToken cancellationToken)
    {
        var report = await healthChecks.CheckHealthAsync(cancellationToken);
        var response = new
        {
            feedId = registry.Feed.FeedId,
            status = report.Status.ToString().ToLowerInvariant(),
            surfaces = registry.Surfaces.Select(surface => new
            {
                surface.SurfaceId,
                protocol = surface.Protocol.ToString(),
                surface.OciSegment,
                surface.RoutePrefix,
                status = report.Status == HealthStatus.Healthy ? "ready" : "not-ready",
            }),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString().ToLowerInvariant(),
                    entry.Value.Description,
                    durationMilliseconds = entry.Value.Duration.TotalMilliseconds,
                }),
            metrics = new
            {
                push = metrics.GetPushCounts(),
                pull = metrics.GetPullCounts(),
                blobBytes = metrics.GetBlobBytes(),
            },
        };

        return Results.Json(
            response,
            statusCode: report.Status == HealthStatus.Healthy
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
    }
}

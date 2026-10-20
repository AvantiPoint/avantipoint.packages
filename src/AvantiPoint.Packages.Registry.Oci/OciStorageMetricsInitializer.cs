using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Metrics;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Oci;

internal sealed class OciStorageMetricsInitializer(
    IServiceScopeFactory scopeFactory,
    IFeedRegistry registry,
    FeedMetricsService metrics,
    ILogger<OciStorageMetricsInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!registry.Surfaces.Any(surface => surface.Protocol == FeedProtocol.Oci))
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var value = await context.OciBlobs
                .AsNoTracking()
                .Where(blob => blob.FeedId == registry.Feed.FeedId)
                .SumAsync(blob => blob.Size, cancellationToken);
            metrics.SetBlobBytes(registry.Feed.FeedId, FeedProtocol.Oci, value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "OCI storage metrics could not be initialized for feed {FeedId}",
                registry.Feed.FeedId);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

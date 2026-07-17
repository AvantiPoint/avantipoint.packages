using AvantiPoint.Feed.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Oci;

internal sealed class OciGarbageCollectionHostedService(
    IServiceScopeFactory scopeFactory,
    IFeedRegistry registry,
    IOptionsMonitor<OciGarbageCollectionOptions> options,
    ILogger<OciGarbageCollectionHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan DisabledPollInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var current = options.CurrentValue;
            if (current.Enabled)
            {
                await CollectAsync(current, stoppingToken);
            }

            var interval = current.Enabled ? current.Interval : DisabledPollInterval;
            if (interval < MinimumInterval)
            {
                interval = MinimumInterval;
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task CollectAsync(
        OciGarbageCollectionOptions current,
        CancellationToken cancellationToken)
    {
        foreach (var surface in registry.Surfaces.Where(surface => surface.Protocol == FeedProtocol.Oci))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var collector = scope.ServiceProvider.GetRequiredService<OciGarbageCollectionService>();
                await collector.CollectAsync(
                    new OciScope(registry.Feed.FeedId, surface.OciSegment),
                    current.DryRun,
                    current.MinimumAge,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "OCI garbage collection failed for feed {FeedId} surface {SurfaceId}",
                    registry.Feed.FeedId,
                    surface.SurfaceId);
            }
        }
    }
}

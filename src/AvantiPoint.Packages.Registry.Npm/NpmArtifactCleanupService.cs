using AvantiPoint.Feed.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Npm;

public sealed class NpmArtifactCleanupService(IServiceScopeFactory scopes, ILogger<NpmArtifactCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Let host startup/migrations finish before touching the catalog.
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var registry = scope.ServiceProvider.GetRequiredService<IFeedRegistry>();
                var service = scope.ServiceProvider.GetRequiredService<NpmPackageService>();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                await service.CleanupUnpublishedArtifactsAsync(registry.Feed.FeedId, timeout.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning("npm artifact cleanup will retry ({ErrorType})", exception.GetType().Name); }
        }
    }
}

using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Registry.Oci;

public sealed class OciUploadCleanupOptions
{
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class OciUploadCleanupService(IContext context, IStorageBackendFactory storageFactory, TimeProvider time, ILogger<OciUploadCleanupService> logger)
{
    public async Task<int> CleanupAsync(string feedId, TimeSpan idleTimeout, CancellationToken cancellationToken = default)
    {
        if (idleTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        var cutoff = time.GetUtcNow().UtcDateTime - idleTimeout;
        // Cancelling idle sessions invalidates concurrent updates through their concurrency token.
        await context.OciUploads.Where(u => u.FeedId == feedId && !u.IsCancelled && (u.LastActivityAt ?? u.CreatedAt) <= cutoff)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.IsCancelled, true), cancellationToken);
        var pending = await context.OciUploads.AsNoTracking().Where(u => u.FeedId == feedId && u.IsCancelled)
            .OrderBy(u => u.Key).Take(100).ToListAsync(cancellationToken);
        var cleaned = 0;
        foreach (var upload in pending)
        {
            try
            {
                var store = storageFactory.CreatePathStore(OciSurfaceOptionsBuilder.GetStorageSubPrefix(upload.OciSegment));
                if (await store.ExistsAsync(upload.StoragePath, cancellationToken))
                    await store.DeleteAsync(upload.StoragePath, cancellationToken);
                await context.OciUploads.Where(u => u.FeedId == feedId && u.Key == upload.Key && u.IsCancelled && u.StoragePath == upload.StoragePath)
                    .ExecuteDeleteAsync(cancellationToken);
                cleaned++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogWarning("OCI upload cleanup {UploadKey} will retry ({ErrorType})", upload.Key, exception.GetType().Name); }
        }
        return cleaned;
    }
}

internal sealed class OciUploadCleanupHostedService(IServiceScopeFactory scopes, IOptionsMonitor<OciUploadCleanupOptions> options, ILogger<OciUploadCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(options.CurrentValue.Interval, stoppingToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                await scope.ServiceProvider.GetRequiredService<OciUploadCleanupService>().CleanupAsync(
                    scope.ServiceProvider.GetRequiredService<IFeedRegistry>().Feed.FeedId, options.CurrentValue.IdleTimeout, timeout.Token);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogWarning("OCI upload cleanup will retry ({ErrorType})", exception.GetType().Name); }
        }
    }
}

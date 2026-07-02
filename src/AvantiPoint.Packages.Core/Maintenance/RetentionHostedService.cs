#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Periodically applies the configured <see cref="RetentionOptions"/> policy.
    /// Does nothing when retention is disabled.
    /// </summary>
    public sealed class RetentionHostedService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<RetentionOptions> options,
        ILogger<RetentionHostedService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!options.CurrentValue.Enabled)
            {
                logger.LogDebug("Package retention is disabled.");
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var retention = scope.ServiceProvider.GetRequiredService<IRetentionPolicyService>();
                    var removed = await retention.ApplyAsync(stoppingToken);
                    if (removed > 0)
                    {
                        logger.LogInformation("Retention scan removed {Count} package version(s)", removed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Retention scan failed");
                }

                var interval = options.CurrentValue.ScanInterval;
                if (interval <= TimeSpan.Zero)
                {
                    interval = TimeSpan.FromHours(24);
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

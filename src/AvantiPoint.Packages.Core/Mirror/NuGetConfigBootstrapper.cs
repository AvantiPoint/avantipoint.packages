#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// On startup, reads an optional NuGet.config file (if configured) and
/// ensures corresponding <see cref="PackageSource"/> rows exist.
/// </summary>
public class NuGetConfigBootstrapper(
    ILogger<NuGetConfigBootstrapper> logger,
    IOptions<MirrorOptions> mirrorOptions,
    IPackageSourceService packageSourceService,
    NuGetConfigParser nugetConfigParser) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configPath = mirrorOptions.Value.NuGetConfigPath;
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return;
        }

        logger.LogInformation("Loading upstream package sources from NuGet.config at {ConfigPath}", configPath);

        var sources = nugetConfigParser.LoadSourcesFromConfig(configPath).ToList();
        if (sources.Count == 0)
        {
            logger.LogInformation("No package sources were loaded from NuGet.config at {ConfigPath}", configPath);
            return;
        }

        var existing = await packageSourceService.GetEnabledUpstreamSourcesAsync(cancellationToken);

        foreach (var source in sources)
        {
            if (existing.Any(s => string.Equals(s.Name, source.Name, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogDebug("Package source {SourceName} already exists, skipping creation.", source.Name);
                continue;
            }

            var packageSource = new PackageSource
            {
                Name = source.Name,
                FeedUrl = source.SourceUrl,
                Type = PackageSourceType.Upstream,
                CachingStrategy = mirrorOptions.Value.DefaultCachingStrategy,
                MirrorSignaturePolicy = mirrorOptions.Value.DefaultSignaturePolicy,
                Username = source.Username,
                Password = source.Password,
                IsEnabled = true
            };

            await packageSourceService.AddAsync(packageSource, cancellationToken);

            logger.LogInformation(
                "Created package source {SourceName} ({FeedUrl}) from NuGet.config",
                packageSource.Name,
                packageSource.FeedUrl);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}



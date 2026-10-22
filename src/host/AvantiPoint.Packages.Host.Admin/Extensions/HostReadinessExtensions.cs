using System.Security.Cryptography;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostReadinessExtensions
{
    public static IServiceCollection AddHostDependencyHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<HostStorageHealthCheck>();
        services.AddSingleton<HostSearchHealthCheck>();
        services.AddHealthChecks()
            .AddCheck<HostStorageHealthCheck>("package_storage", timeout: TimeSpan.FromSeconds(10))
            .AddCheck<HostSearchHealthCheck>("package_search", timeout: TimeSpan.FromSeconds(10));
        return services;
    }
}

/// <summary>Coalesces concurrent probes and caches results briefly to bound dependency traffic.</summary>
public abstract class CachedHostDependencyHealthCheck(IServiceScopeFactory scopes) : IHealthCheck
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HealthCheckResult _cached;
    private DateTimeOffset _expires;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_expires > DateTimeOffset.UtcNow) return _cached;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await ProbeAsync(scope.ServiceProvider, cancellationToken);
                _cached = HealthCheckResult.Healthy();
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Exception details are available to operators, not the public readiness response.
                _cached = HealthCheckResult.Unhealthy("The configured dependency failed its readiness probe.", exception);
            }
            _expires = DateTimeOffset.UtcNow.AddSeconds(15);
            return _cached;
        }
        finally { _gate.Release(); }
    }

    protected abstract Task ProbeAsync(IServiceProvider services, CancellationToken cancellationToken);
}

public sealed class HostSearchHealthCheck(IServiceScopeFactory scopes) : CachedHostDependencyHealthCheck(scopes)
{
    protected override async Task ProbeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Autocomplete uses only the configured local search provider, never upstream federation.
        await services.GetRequiredService<ISearchService>().AutocompleteAsync(new AutocompleteRequest
        {
            Query = "avantipoint-readiness-probe", Take = 1, IncludeSemVer2 = true,
        }, cancellationToken);
    }
}

public sealed class HostStorageHealthCheck(IServiceScopeFactory scopes) : CachedHostDependencyHealthCheck(scopes)
{
    private readonly string _probeName = $"{Guid.NewGuid():N}.probe";

    protected override async Task ProbeAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var store = services.GetRequiredService<IStorageBackendFactory>().CreatePathStore("_health");
        var expected = RandomNumberGenerator.GetBytes(32);
        try
        {
            await using var content = new MemoryStream(expected, writable: false);
            await store.PutAsync(_probeName, content, cancellationToken);
            await using var read = await store.GetAsync(_probeName, cancellationToken);
            var actual = new byte[expected.Length];
            await read.ReadExactlyAsync(actual, cancellationToken);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                throw new IOException("Storage did not return the data written by the readiness probe.");
        }
        finally
        {
            // A separate, short cleanup token also removes partial writes after probe cancellation.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await store.DeleteAsync(_probeName, cleanup.Token);
        }
    }
}

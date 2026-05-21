using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AvantiPoint.Packages.Host.Extensions;

public static class HostDatabaseHealthCheckExtensions
{
    public static IServiceCollection AddHostDatabaseHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PackageCatalogDbHealthCheck>("package_catalog_db")
            .AddCheck<HostIdentityDbHealthCheck>("host_identity_db");
        return services;
    }

    private sealed class PackageCatalogDbHealthCheck(IServiceProvider services) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Package catalog database is reachable.")
                : HealthCheckResult.Unhealthy("Package catalog database is not reachable.");
        }
    }

    private sealed class HostIdentityDbHealthCheck(IServiceProvider services) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IHostIdentityContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Host identity database is reachable.")
                : HealthCheckResult.Unhealthy("Host identity database is not reachable.");
        }
    }
}

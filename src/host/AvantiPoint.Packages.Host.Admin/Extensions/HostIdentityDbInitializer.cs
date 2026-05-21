using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostIdentityDbInitializer
{
    public static async Task InitializeHostDatabasesAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("HostDatabaseInitializer");

        var packageContext = scope.ServiceProvider.GetRequiredService<IContext>();
        if (packageContext is DbContext packageDb)
        {
            await MigrateContextAsync(packageDb, logger, cancellationToken);
        }

        var hostContext = scope.ServiceProvider.GetRequiredService<IHostIdentityContext>();
        if (hostContext is DbContext hostDb)
        {
            await MigrateContextAsync(hostDb, logger, cancellationToken);
        }

        await EnsureAccessSettingsAsync(scope.ServiceProvider, cancellationToken);
    }

    private static async Task MigrateContextAsync(
        DbContext context,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var contextName = context.GetType().Name;
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} migration(s) to {ContextName}", pending.Count, contextName);
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            logger.LogInformation("No pending migrations for {ContextName}", contextName);
        }
    }

    private static async Task EnsureAccessSettingsAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var identity = services.GetRequiredService<IHostIdentityContext>();
        if (!await identity.HostAccessSettings.AnyAsync(cancellationToken))
        {
            identity.HostAccessSettings.Add(new HostAccessSettings { Id = 1, RequireNewUserApproval = false });
            await identity.SaveChangesAsync(cancellationToken);
        }
    }
}

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
        await ProtectStoredSecretsAsync(scope.ServiceProvider, logger, cancellationToken);
    }

    /// <summary>
    /// One-time transparent migration: re-encrypts any legacy plaintext credentials
    /// (upstream package source secrets and downstream publish tokens) using the
    /// registered <see cref="ISecretProtector"/>.
    /// </summary>
    private static async Task ProtectStoredSecretsAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var protector = services.GetRequiredService<ISecretProtector>();
        if (protector is NullSecretProtector)
        {
            return;
        }

        var sourcesMigrated = 0;
        var packageContext = services.GetRequiredService<IContext>();
        var sources = await packageContext.PackageSources.ToListAsync(cancellationToken);
        foreach (var source in sources)
        {
            if (protector.IsProtected(source.Username)
                && protector.IsProtected(source.Password)
                && protector.IsProtected(source.ApiKey))
            {
                continue;
            }

            source.Username = protector.Protect(source.Username);
            source.Password = protector.Protect(source.Password);
            source.ApiKey = protector.Protect(source.ApiKey);
            sourcesMigrated++;
        }

        if (sourcesMigrated > 0)
        {
            await packageContext.SaveChangesAsync(cancellationToken);
        }

        var targetsMigrated = 0;
        var identity = services.GetRequiredService<IHostIdentityContext>();
        var targets = await identity.HostPublishTargets.ToListAsync(cancellationToken);
        foreach (var target in targets)
        {
            if (protector.IsProtected(target.ApiToken))
            {
                continue;
            }

            target.ApiToken = protector.Protect(target.ApiToken)!;
            targetsMigrated++;
        }

        if (targetsMigrated > 0)
        {
            await identity.SaveChangesAsync(cancellationToken);
        }

        if (sourcesMigrated > 0 || targetsMigrated > 0)
        {
            logger.LogInformation(
                "Encrypted stored credentials for {SourceCount} package source(s) and {TargetCount} publish target(s)",
                sourcesMigrated,
                targetsMigrated);
        }
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

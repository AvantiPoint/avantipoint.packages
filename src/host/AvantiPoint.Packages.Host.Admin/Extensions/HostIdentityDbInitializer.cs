using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Upstreams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Host.Admin.Extensions;

public static class HostIdentityDbInitializer
{
    public static async Task InitializeHostDatabasesAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("HostDatabaseInitializer");

        WarnIfDataProtectionKeyPathMissing(scope.ServiceProvider, logger);
        WarnIfTokenHashPepperIsNotConfigured(scope.ServiceProvider, logger);

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

        await scope.ServiceProvider
            .GetRequiredService<ConfigurationPackageSourceSeeder>()
            .ApplyAsync(cancellationToken);
        await EnsureAccessSettingsAsync(scope.ServiceProvider, cancellationToken);
        await ProtectStoredSecretsAsync(scope.ServiceProvider, logger, cancellationToken);
    }

    /// <summary>
    /// Warns at startup when no durable Data Protection key path is configured. Without one,
    /// the key ring may not survive a restart (especially in a container), which makes every
    /// previously-encrypted credential (upstream/downstream secrets) unreadable.
    /// </summary>
    private static void WarnIfDataProtectionKeyPathMissing(IServiceProvider services, ILogger logger)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        if (string.IsNullOrWhiteSpace(configuration["Host:DataProtection:KeyPath"]))
        {
            logger.LogWarning(
                "Host:DataProtection:KeyPath is not configured. The Data Protection key ring may not " +
                "persist across restarts, which will make previously-encrypted feed credentials " +
                "unreadable. Configure Host:DataProtection:KeyPath to a durable, shared directory " +
                "(for example the same volume as the database).");
        }
    }

    /// <summary>
    /// API tokens contain 256 bits of randomness, but a deployment-specific pepper still provides
    /// defense in depth if the identity database is copied. Keep the development/default values
    /// usable for local evaluation while making a production misconfiguration visible at startup.
    /// </summary>
    private static void WarnIfTokenHashPepperIsNotConfigured(IServiceProvider services, ILogger logger)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var pepper = configuration["Host:TokenHashPepper"];
        if (string.IsNullOrWhiteSpace(pepper)
            || string.Equals(pepper, "change-me-in-production", StringComparison.Ordinal)
            || string.Equals(pepper, "development-only-not-for-production", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Host:TokenHashPepper is missing or still uses a development/default value. " +
                "Set Host__TokenHashPepper to a stable, deployment-specific secret before issuing API tokens. " +
                "Changing it later invalidates existing tokens.");
        }
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
            var access = services.GetRequiredService<IOptions<HostAccessOptions>>().Value;
            identity.HostAccessSettings.Add(new HostAccessSettings { Id = 1, RequireNewUserApproval = access.RequireNewUserApproval });
            await identity.SaveChangesAsync(cancellationToken);
        }
    }
}

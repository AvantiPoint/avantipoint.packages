using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Core.Discovery;

/// <summary>
/// Base class for database context service providers that supports both auto-discovery
/// (on-demand creation) and explicit registration (using pre-registered DbContext).
/// </summary>
public abstract class DatabaseContextServiceProvider<TContext>(IServiceProvider services)
    : ServiceDiscoveryProvider<IContext, TContext>(services)
    where TContext : DbContext, IContext
{
    /// <summary>
    /// Configures the DbContextOptionsBuilder for on-demand creation.
    /// Only called when no explicit DbContext registration exists.
    /// </summary>
    protected abstract void ConfigureBuilder(IServiceProvider services, DbContextOptionsBuilder<TContext> builder);

    public override IContext GetService()
    {
        // Try to use explicitly registered DbContext first (explicit configuration mode)
        // GetService returns null if not registered, GetRequiredService throws
        var registeredContext = Services.GetService<TContext>();
        if (registeredContext != null)
        {
            return registeredContext;
        }

        // Fall back to on-demand creation (auto-discovery mode)
        var builder = new DbContextOptionsBuilder<TContext>();
        ConfigureBuilder(Services, builder);
        return ActivatorUtilities.CreateInstance<TContext>(Services, builder.Options);
    }

    public override void ValidateConfiguration()
    {
        // If DbContext is explicitly registered, validation happens during registration
        // Just verify it can be resolved
        var registeredContext = Services.GetService<TContext>();
        if (registeredContext != null)
        {
            return;
        }

        // Otherwise validate on-demand creation configuration
        var builder = new DbContextOptionsBuilder<TContext>();
        ConfigureBuilder(Services, builder);
    }
}


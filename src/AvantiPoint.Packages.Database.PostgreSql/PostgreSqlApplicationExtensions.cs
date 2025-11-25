using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Database.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages;

/// <summary>
/// Extension methods for explicitly adding PostgreSQL database support.
/// When using AddPostgreSqlDatabase, PostgreSQL will always be used regardless of configuration.
/// </summary>
public static class PostgreSqlApplicationExtensions
{
    /// <summary>
    /// Explicitly adds PostgreSQL database support.
    /// The database will be configured from <see cref="DatabaseOptions"/> in configuration.
    /// PostgreSQL will be used regardless of the Database:Type configuration value.
    /// </summary>
    public static NuGetApiOptions AddPostgreSqlDatabase(this NuGetApiOptions options)
    {
        // Register DbContext using options from configuration
        options.Services.AddDbContext<PostgreSqlContext>((sp, builder) =>
        {
            var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
            if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
            {
                throw new InvalidOperationException("Database:ConnectionString must be configured for PostgreSql.");
            }
            builder.UseNpgsql(dbOptions.ConnectionString);
        });

        // Register search support and provider
        options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.PostgreSql);
        options.Services.AddScoped<IContextServiceProvider, PostgreSqlContextServiceProvider>();

        return options;
    }

    /// <summary>
    /// Explicitly adds PostgreSQL database support with custom options configuration.
    /// PostgreSQL will be used regardless of the Database:Type configuration value.
    /// </summary>
    public static NuGetApiOptions AddPostgreSqlDatabase(
        this NuGetApiOptions options,
        Action<DatabaseOptions> configure)
    {
        options.Services.Configure(configure);
        return options.AddPostgreSqlDatabase();
    }

    /// <summary>
    /// Explicitly adds PostgreSQL database support using a connection string from configuration.
    /// PostgreSQL will be used regardless of the Database:Type configuration value.
    /// </summary>
    public static NuGetApiOptions AddPostgreSqlDatabase(
        this NuGetApiOptions options,
        string connectionStringName)
    {
        return options.AddPostgreSqlDatabase(o =>
            o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
    }

    /// <summary>
    /// Explicitly adds PostgreSQL database support with full DbContext builder control.
    /// This allows full control over DbContext configuration, including retry policies, logging, etc.
    /// PostgreSQL will be used regardless of the Database:Type configuration value.
    /// </summary>
    /// <param name="options">The NuGet API options.</param>
    /// <param name="configureDbContext">Action to configure the DbContext options builder.</param>
    /// <returns>The NuGet API options for chaining.</returns>
    public static NuGetApiOptions AddPostgreSqlDatabase(
        this NuGetApiOptions options,
        Action<IServiceProvider, DbContextOptionsBuilder<PostgreSqlContext>> configureDbContext)
    {
        // Register the DbContext explicitly with full builder control
        options.Services.AddDbContext<PostgreSqlContext>((sp, b) => configureDbContext(sp, (DbContextOptionsBuilder<PostgreSqlContext>)b));

        // Register search support and provider (provider will use the registered DbContext)
        options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.PostgreSql);
        options.Services.AddScoped<IContextServiceProvider, PostgreSqlContextServiceProvider>();

        return options;
    }

    /// <summary>
    /// Explicitly adds PostgreSQL database support with DbContext builder control and options configuration.
    /// PostgreSQL will be used regardless of the Database:Type configuration value.
    /// </summary>
    public static NuGetApiOptions AddPostgreSqlDatabase(
        this NuGetApiOptions options,
        Action<IServiceProvider, DbContextOptionsBuilder<PostgreSqlContext>> configureDbContext,
        Action<DatabaseOptions> configureOptions)
    {
        options.Services.Configure(configureOptions);
        return options.AddPostgreSqlDatabase(configureDbContext);
    }

    /// <summary>
    /// Registers PostgreSQL database provider for auto-discovery mode.
    /// The provider will be selected based on Database:Type configuration.
    /// Does not register the DbContext - it will be created on-demand by the provider.
    /// </summary>
    public static NuGetApiOptions AutoDiscoverPostgreSqlDatabase(this NuGetApiOptions options)
    {
        options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.PostgreSql);
        options.Services.AddScoped<IContextServiceProvider, PostgreSqlContextServiceProvider>();
        return options;
    }
}


using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages
{
    /// <summary>
    /// Extension methods for explicitly adding SQLite database support.
    /// When using AddSqliteDatabase, SQLite will always be used regardless of configuration.
    /// </summary>
    public static class SqliteApplicationExtensions
    {
        /// <summary>
        /// Explicitly adds SQLite database support.
        /// The database will be configured from <see cref="DatabaseOptions"/> in configuration.
        /// SQLite will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqliteDatabase(this NuGetApiOptions options)
        {
            // Register DbContext using options from configuration
            options.Services.AddDbContext<SqliteContext>((sp, builder) =>
            {
                var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
                if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
                {
                    throw new InvalidOperationException("Database:ConnectionString must be configured for Sqlite.");
                }
                builder.UseSqlite(dbOptions.ConnectionString);
            });

            // Register search support and provider
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.Sqlite);
            options.Services.AddScoped<IContextServiceProvider, SqliteContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds SQLite database support with custom options configuration.
        /// SQLite will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqliteDatabase(
            this NuGetApiOptions options,
            Action<DatabaseOptions> configure)
        {
            options.Services.Configure(configure);
            return options.AddSqliteDatabase();
        }

        /// <summary>
        /// Explicitly adds SQLite database support using a connection string from configuration.
        /// SQLite will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqliteDatabase(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddSqliteDatabase(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }

        /// <summary>
        /// Explicitly adds SQLite database support with full DbContext builder control.
        /// This allows full control over DbContext configuration, including retry policies, logging, etc.
        /// SQLite will be used regardless of the Database:Type configuration value.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="configureDbContext">Action to configure the DbContext options builder.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddSqliteDatabase(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<SqliteContext>> configureDbContext)
        {
            // Register the DbContext explicitly with full builder control
            options.Services.AddDbContext<SqliteContext>((sp, b) => configureDbContext(sp, (DbContextOptionsBuilder<SqliteContext>)b));

            // Register search support and provider (provider will use the registered DbContext)
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.Sqlite);
            options.Services.AddScoped<IContextServiceProvider, SqliteContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds SQLite database support with DbContext builder control and options configuration.
        /// SQLite will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqliteDatabase(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<SqliteContext>> configureDbContext,
            Action<DatabaseOptions> configureOptions)
        {
            options.Services.Configure(configureOptions);
            return options.AddSqliteDatabase(configureDbContext);
        }

        /// <summary>
        /// Registers SQLite database provider for auto-discovery mode.
        /// The provider will be selected based on Database:Type configuration.
        /// Does not register the DbContext - it will be created on-demand by the provider.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverSqliteDatabase(this NuGetApiOptions options)
        {
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.Sqlite);
            options.Services.AddScoped<IContextServiceProvider, SqliteContextServiceProvider>();
            return options;
        }
    }
}

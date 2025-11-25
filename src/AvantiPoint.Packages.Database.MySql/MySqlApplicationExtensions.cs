using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Database.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages
{
    /// <summary>
    /// Extension methods for explicitly adding MySQL/MariaDB database support.
    /// When using AddMySqlDatabase or AddMariaDb, the selected database will always be used regardless of configuration.
    /// </summary>
    public static class MySqlApplicationExtensions
    {
        /// <summary>
        /// Explicitly adds MySQL database support.
        /// The database will be configured from <see cref="MySqlDatabaseOptions"/> in configuration.
        /// MySQL will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMySqlDatabase(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));

            // Register DbContext using options from configuration
            options.Services.AddDbContext<MySqlContext>((sp, builder) =>
            {
                var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MySqlDatabaseOptions>>().Value;
                if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
                {
                    throw new InvalidOperationException("Database:ConnectionString must be configured for MySql.");
                }
                if (string.IsNullOrWhiteSpace(dbOptions.Version))
                {
                    throw new InvalidOperationException("Database:Version must be configured for MySql.");
                }
                var version = new Pomelo.EntityFrameworkCore.MySql.Infrastructure.MySqlServerVersion(dbOptions.Version);
                builder.UseMySql(dbOptions.ConnectionString, version);
            });

            // Register search support and provider
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.MySql);
            options.Services.AddScoped<IContextServiceProvider, MySqlContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds MySQL database support with custom options configuration.
        /// MySQL will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMySqlDatabase(
            this NuGetApiOptions options,
            Action<MySqlDatabaseOptions> configure)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.Configure(configure);
            return options.AddMySqlDatabase();
        }

        /// <summary>
        /// Explicitly adds MySQL database support using a connection string from configuration.
        /// MySQL will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMySqlDatabase(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddMySqlDatabase(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }

        /// <summary>
        /// Explicitly adds MySQL database support with full DbContext builder control.
        /// This allows full control over DbContext configuration, including retry policies, logging, etc.
        /// MySQL will be used regardless of the Database:Type configuration value.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="configureDbContext">Action to configure the DbContext options builder.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddMySqlDatabase(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<MySqlContext>> configureDbContext)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));

            // Register the DbContext explicitly with full builder control
            options.Services.AddDbContext<MySqlContext>((sp, b) => configureDbContext(sp, (DbContextOptionsBuilder<MySqlContext>)b));

            // Register search support and provider (provider will use the registered DbContext)
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.MySql);
            options.Services.AddScoped<IContextServiceProvider, MySqlContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds MySQL database support with DbContext builder control and options configuration.
        /// MySQL will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMySqlDatabase(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<MySqlContext>> configureDbContext,
            Action<MySqlDatabaseOptions> configureOptions)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.Configure(configureOptions);
            return options.AddMySqlDatabase(configureDbContext);
        }

        /// <summary>
        /// Explicitly adds MariaDB database support.
        /// The database will be configured from <see cref="MySqlDatabaseOptions"/> in configuration.
        /// MariaDB will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMariaDb(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));

            // Register DbContext using options from configuration
            options.Services.AddDbContext<MySqlContext>((sp, builder) =>
            {
                var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MySqlDatabaseOptions>>().Value;
                if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
                {
                    throw new InvalidOperationException("Database:ConnectionString must be configured for MariaDb.");
                }
                if (string.IsNullOrWhiteSpace(dbOptions.Version))
                {
                    throw new InvalidOperationException("Database:Version must be configured for MariaDb.");
                }
                var version = new Pomelo.EntityFrameworkCore.MySql.Infrastructure.MariaDbServerVersion(dbOptions.Version);
                builder.UseMySql(dbOptions.ConnectionString, version);
            });

            // Register search support and provider
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.MariaDb);
            options.Services.AddScoped<IContextServiceProvider, MariaDbContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds MariaDB database support with custom options configuration.
        /// MariaDB will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMariaDb(
            this NuGetApiOptions options,
            Action<MySqlDatabaseOptions> configure)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.Configure(configure);
            return options.AddMariaDb();
        }

        /// <summary>
        /// Explicitly adds MariaDB database support using a connection string from configuration.
        /// MariaDB will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMariaDb(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddMariaDb(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }

        /// <summary>
        /// Explicitly adds MariaDB database support with full DbContext builder control.
        /// This allows full control over DbContext configuration, including retry policies, logging, etc.
        /// MariaDB will be used regardless of the Database:Type configuration value.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="configureDbContext">Action to configure the DbContext options builder.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddMariaDb(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<MySqlContext>> configureDbContext)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));

            // Register the DbContext explicitly with full builder control
            options.Services.AddDbContext<MySqlContext>((sp, b) => configureDbContext(sp, (DbContextOptionsBuilder<MySqlContext>)b));

            // Register search support and provider (provider will use the registered DbContext)
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.MariaDb);
            options.Services.AddScoped<IContextServiceProvider, MariaDbContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds MariaDB database support with DbContext builder control and options configuration.
        /// MariaDB will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddMariaDb(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<MySqlContext>> configureDbContext,
            Action<MySqlDatabaseOptions> configureOptions)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.Configure(configureOptions);
            return options.AddMariaDb(configureDbContext);
        }

        /// <summary>
        /// Registers MySQL database provider for auto-discovery mode.
        /// The provider will be selected based on Database:Type configuration.
        /// Does not register the DbContext - it will be created on-demand by the provider.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverMySqlDatabase(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.MySql);
            options.Services.AddScoped<IContextServiceProvider, MySqlContextServiceProvider>();
            return options;
        }

        /// <summary>
        /// Registers MariaDB database provider for auto-discovery mode.
        /// The provider will be selected based on Database:Type configuration.
        /// Does not register the DbContext - it will be created on-demand by the provider.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverMariaDb(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.MariaDb);
            options.Services.AddScoped<IContextServiceProvider, MariaDbContextServiceProvider>();
            return options;
        }
    }
}
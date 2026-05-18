using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Discovery;
using AvantiPoint.Packages.Database.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages
{
    /// <summary>
    /// Extension methods for explicitly adding SQL Server database support.
    /// When using AddSqlServerDatabase, SQL Server will always be used regardless of configuration.
    /// </summary>
    public static class SqlServerApplicationExtensions
    {
        /// <summary>
        /// Explicitly adds SQL Server database support.
        /// The database will be configured from <see cref="DatabaseOptions"/> in configuration.
        /// SQL Server will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqlServerDatabase(this NuGetApiOptions options)
        {
            // Register DbContext using options from configuration
            options.Services.AddDbContext<SqlServerContext>((sp, builder) =>
            {
                var dbOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>().Value;
                if (string.IsNullOrWhiteSpace(dbOptions.ConnectionString))
                {
                    throw new InvalidOperationException("Database:ConnectionString must be configured for SqlServer.");
                }
                builder.UseSqlServer(dbOptions.ConnectionString);
            });

            // Register search support and provider
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.SqlServer);
            options.Services.AddScoped<IContextServiceProvider, SqlServerContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds SQL Server database support with custom options configuration.
        /// SQL Server will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqlServerDatabase(
            this NuGetApiOptions options,
            Action<DatabaseOptions> configure)
        {
            options.Services.Configure(configure);
            return options.AddSqlServerDatabase();
        }

        /// <summary>
        /// Explicitly adds SQL Server database support using a connection string from configuration.
        /// SQL Server will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqlServerDatabase(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddSqlServerDatabase(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }

        /// <summary>
        /// Explicitly adds SQL Server database support with full DbContext builder control.
        /// This allows full control over DbContext configuration, including retry policies, logging, etc.
        /// SQL Server will be used regardless of the Database:Type configuration value.
        /// </summary>
        /// <param name="options">The NuGet API options.</param>
        /// <param name="configureDbContext">Action to configure the DbContext options builder.</param>
        /// <returns>The NuGet API options for chaining.</returns>
        public static NuGetApiOptions AddSqlServerDatabase(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<SqlServerContext>> configureDbContext)
        {
            // Register the DbContext explicitly with full builder control
            options.Services.AddDbContext<SqlServerContext>((sp, b) => configureDbContext(sp, (DbContextOptionsBuilder<SqlServerContext>)b));

            // Register search support and provider (provider will use the registered DbContext)
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.SqlServer);
            options.Services.AddScoped<IContextServiceProvider, SqlServerContextServiceProvider>();

            return options;
        }

        /// <summary>
        /// Explicitly adds SQL Server database support with DbContext builder control and options configuration.
        /// SQL Server will be used regardless of the Database:Type configuration value.
        /// </summary>
        public static NuGetApiOptions AddSqlServerDatabase(
            this NuGetApiOptions options,
            Action<IServiceProvider, DbContextOptionsBuilder<SqlServerContext>> configureDbContext,
            Action<DatabaseOptions> configureOptions)
        {
            options.Services.Configure(configureOptions);
            return options.AddSqlServerDatabase(configureDbContext);
        }

        /// <summary>
        /// Registers SQL Server database provider for auto-discovery mode.
        /// The provider will be selected based on Database:Type configuration.
        /// Does not register the DbContext - it will be created on-demand by the provider.
        /// </summary>
        public static NuGetApiOptions AutoDiscoverSqlServerDatabase(this NuGetApiOptions options)
        {
            options.Services.AddDatabaseSearchSupport(DatabaseProviderNames.SqlServer);
            options.Services.AddScoped<IContextServiceProvider, SqlServerContextServiceProvider>();
            return options;
        }
    }
}

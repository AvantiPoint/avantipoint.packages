using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class MySqlApplicationExtensions
    {
        public static NuGetApiOptions AddMySqlDatabase(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.AddNuGetFeedDbContextProvider<MySqlContext>("MySql", (provider, builder) =>
            {
                var databaseOptions = provider.GetRequiredService<IOptionsSnapshot<MySqlDatabaseOptions>>();
                var version = new MySqlServerVersion(databaseOptions.Value.Version);
                builder.UseMySql(databaseOptions.Value.ConnectionString, version);
            });

            return options;
        }

        public static NuGetApiOptions AddMySqlDatabase(
            this NuGetApiOptions options,
            Action<MySqlDatabaseOptions> configure)
        {
            options.AddMySqlDatabase();
            options.Services.Configure(configure);
            return options;
        }

        public static NuGetApiOptions AddMySqlDatabase(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddMySqlDatabase(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }

        public static NuGetApiOptions AddMariaDb(this NuGetApiOptions options)
        {
            options.Services.AddNuGetApiOptions<MySqlDatabaseOptions>(nameof(PackageFeedOptions.Database));
            options.Services.AddNuGetFeedDbContextProvider<MySqlContext>("MariaDb", (provider, builder) =>
            {
                var databaseOptions = provider.GetRequiredService<IOptionsSnapshot<MySqlDatabaseOptions>>();
                var version = new MariaDbServerVersion(databaseOptions.Value.Version);
                builder.UseMySql(databaseOptions.Value.ConnectionString, version);
            });

            return options;
        }

        public static NuGetApiOptions AddMariaDb(
            this NuGetApiOptions options,
            Action<MySqlDatabaseOptions> configure)
        {
            options.AddMariaDb();
            options.Services.Configure(configure);
            return options;
        }

        public static NuGetApiOptions AddMariaDb(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddMariaDb(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }
    }
}
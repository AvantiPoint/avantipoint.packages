using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class SqliteApplicationExtensions
    {
        public static NuGetApiOptions AddSqliteDatabase(this NuGetApiOptions options)
        {
            options.Services.AddNuGetFeedDbContextProvider<SqliteContext>("Sqlite", (provider, builder) =>
            {
                var databaseOptions = provider.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>();

                builder.UseSqlite(databaseOptions.Value.ConnectionString);
            });

            return options;
        }

        public static NuGetApiOptions AddSqliteDatabase(
            this NuGetApiOptions options,
            Action<DatabaseOptions> configure)
        {
            options.AddSqliteDatabase();
            options.Services.Configure(configure);
            return options;
        }

        public static NuGetApiOptions AddSqliteDatabase(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddSqliteDatabase(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }
    }
}
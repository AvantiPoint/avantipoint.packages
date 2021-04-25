using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class SqlServerApplicationExtensions
    {
        public static NuGetApiOptions AddSqlServerDatabase(this NuGetApiOptions options)
        {
            options.Services.AddNuGetFeedDbContextProvider<SqlServerContext>("SqlServer", (provider, builder) =>
            {
                var databaseOptions = provider.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>();

                builder.UseSqlServer(databaseOptions.Value.ConnectionString);
            });

            return options;
        }

        public static NuGetApiOptions AddSqlServerDatabase(
            this NuGetApiOptions options,
            Action<DatabaseOptions> configure)
        {
            options.AddSqlServerDatabase();
            options.Services.Configure(configure);
            return options;
        }

        public static NuGetApiOptions AddSqlServerDatabase(
            this NuGetApiOptions options,
            string connectionStringName)
        {
            return options.AddSqlServerDatabase(o =>
                o.ConnectionString = options.Configuration.GetConnectionString(connectionStringName));
        }
    }
}

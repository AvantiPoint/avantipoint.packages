using System;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages
{
    public static class SqlServerApplicationExtensions
    {
        public static NuGetApiApplication AddSqlServerDatabase(this NuGetApiApplication app)
        {
            app.Services.AddNuGetFeedDbContextProvider<SqlServerContext>("SqlServer", (provider, options) =>
            {
                var databaseOptions = provider.GetRequiredService<IOptionsSnapshot<DatabaseOptions>>();

                options.UseSqlServer(databaseOptions.Value.ConnectionString);
            });

            return app;
        }

        public static NuGetApiApplication AddSqlServerDatabase(
            this NuGetApiApplication app,
            Action<DatabaseOptions> configure)
        {
            app.AddSqlServerDatabase();
            app.Services.Configure(configure);
            return app;
        }
    }
}

using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Database.Sqlite;

public static class HostSqliteIdentityExtensions
{
    public static IServiceCollection AddHostSqliteIdentityContext(this IServiceCollection services)
    {
        services.AddDbContext<HostSqliteContext>((sp, builder) =>
        {
            var connectionString = HostIdentityDatabaseConfiguration.GetConnectionString(sp);
            builder.UseSqlite(connectionString, sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        });

        services.AddScoped<IHostIdentityContext>(sp => sp.GetRequiredService<HostSqliteContext>());
        services.AddScoped<IHostIdentityContextServiceProvider, HostSqliteContextServiceProvider>();
        return services;
    }
}

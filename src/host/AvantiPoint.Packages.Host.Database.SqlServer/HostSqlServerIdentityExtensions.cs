using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Database.SqlServer;

public static class HostSqlServerIdentityExtensions
{
    public static IServiceCollection AddHostSqlServerIdentityContext(this IServiceCollection services)
    {
        services.AddDbContext<HostSqlServerContext>((sp, builder) =>
        {
            var connectionString = HostIdentityDatabaseConfiguration.GetConnectionString(sp);
            builder.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        });

        services.AddScoped<IHostIdentityContext>(sp => sp.GetRequiredService<HostSqlServerContext>());
        services.AddScoped<IHostIdentityContextServiceProvider, HostSqlServerContextServiceProvider>();
        return services;
    }
}

using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Database.MySql;

public static class HostMySqlIdentityExtensions
{
    public static IServiceCollection AddHostMySqlIdentityContext(this IServiceCollection services)
    {
        services.AddDbContext<HostMySqlContext>((sp, builder) =>
        {
            var connectionString = HostIdentityDatabaseConfiguration.GetConnectionString(sp);
            builder.UseMySQL(connectionString, mySql => mySql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        });

        services.AddScoped<IHostIdentityContext>(sp => sp.GetRequiredService<HostMySqlContext>());
        services.AddScoped<IHostIdentityContextServiceProvider, HostMySqlContextServiceProvider>();
        return services;
    }
}

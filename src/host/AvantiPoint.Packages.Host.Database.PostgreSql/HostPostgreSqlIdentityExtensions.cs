using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Host.Database.PostgreSql;

public static class HostPostgreSqlIdentityExtensions
{
    public static IServiceCollection AddHostPostgreSqlIdentityContext(this IServiceCollection services)
    {
        services.AddDbContext<HostPostgreSqlContext>((sp, builder) =>
        {
            var connectionString = HostIdentityDatabaseConfiguration.GetConnectionString(sp);
            builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        });

        services.AddScoped<IHostIdentityContext>(sp => sp.GetRequiredService<HostPostgreSqlContext>());
        services.AddScoped<IHostIdentityContextServiceProvider, HostPostgreSqlContextServiceProvider>();
        return services;
    }
}

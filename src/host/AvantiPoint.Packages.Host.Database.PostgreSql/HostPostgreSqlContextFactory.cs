using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Host.Database.PostgreSql;

public class HostPostgreSqlContextFactory : IDesignTimeDbContextFactory<HostPostgreSqlContext>
{
    public HostPostgreSqlContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HostPostgreSqlContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=avantipoint_host_identity;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        return new HostPostgreSqlContext(optionsBuilder.Options);
    }
}

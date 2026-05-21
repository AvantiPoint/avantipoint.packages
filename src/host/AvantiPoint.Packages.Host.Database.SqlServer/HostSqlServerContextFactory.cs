using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Host.Database.SqlServer;

public class HostSqlServerContextFactory : IDesignTimeDbContextFactory<HostSqlServerContext>
{
    public HostSqlServerContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HostSqlServerContext>();
        optionsBuilder.UseSqlServer(
            "Server=(localdb)\\mssqllocaldb;Database=AvantiPointHostIdentity;Trusted_Connection=True;",
            sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        return new HostSqlServerContext(optionsBuilder.Options);
    }
}

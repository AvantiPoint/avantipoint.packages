using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Host.Database.Sqlite;

public class HostSqliteContextFactory : IDesignTimeDbContextFactory<HostSqliteContext>
{
    public HostSqliteContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HostSqliteContext>();
        optionsBuilder.UseSqlite(
            "Data Source=avantipoint_host_identity.db",
            sql => sql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        return new HostSqliteContext(optionsBuilder.Options);
    }
}

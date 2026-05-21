using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Host.Database.MySql;

public class HostMySqlContextFactory : IDesignTimeDbContextFactory<HostMySqlContext>
{
    public HostMySqlContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HostMySqlContext>();
        optionsBuilder.UseMySQL(
            "Server=localhost;Database=avantipoint_host_identity;User=root;Password=password;",
            mySql => mySql.MigrationsHistoryTable("__HostIdentityMigrationsHistory"));
        return new HostMySqlContext(optionsBuilder.Options);
    }
}

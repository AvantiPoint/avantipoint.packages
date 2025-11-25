using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Database.PostgreSql;

/// <summary>
/// Design-time factory for creating PostgreSqlContext instances for migrations.
/// </summary>
public class PostgreSqlContextFactory : IDesignTimeDbContextFactory<PostgreSqlContext>
{
    public PostgreSqlContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlContext>();
        
        // Use a dummy connection string for design time
        optionsBuilder.UseNpgsql("Host=localhost;Database=avantipoint_packages;Username=postgres;Password=postgres");

        return new PostgreSqlContext(optionsBuilder.Options);
    }
}


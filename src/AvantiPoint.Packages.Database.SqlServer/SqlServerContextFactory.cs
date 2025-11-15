using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Database.SqlServer
{
    /// <summary>
    /// Design-time factory for creating SqlServerContext instances for migrations.
    /// </summary>
    public class SqlServerContextFactory : IDesignTimeDbContextFactory<SqlServerContext>
    {
        public SqlServerContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqlServerContext>();
            
            // Use a dummy connection string for design time
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AvantiPointPackages;Trusted_Connection=True;");

            return new SqlServerContext(optionsBuilder.Options);
        }
    }
}

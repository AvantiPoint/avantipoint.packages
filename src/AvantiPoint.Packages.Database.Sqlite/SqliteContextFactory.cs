using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Database.Sqlite
{
    /// <summary>
    /// Design-time factory for creating SqliteContext instances for migrations.
    /// </summary>
    public class SqliteContextFactory : IDesignTimeDbContextFactory<SqliteContext>
    {
        public SqliteContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SqliteContext>();
            
            // Use a dummy connection string for design time
            optionsBuilder.UseSqlite("Data Source=avantipoint_packages.db");

            return new SqliteContext(optionsBuilder.Options);
        }
    }
}

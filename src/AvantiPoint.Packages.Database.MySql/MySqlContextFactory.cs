using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AvantiPoint.Packages.Database.MySql
{
    /// <summary>
    /// Design-time factory for creating MySqlContext instances for migrations.
    /// </summary>
    public class MySqlContextFactory : IDesignTimeDbContextFactory<MySqlContext>
    {
        public MySqlContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MySqlContext>();
            
            // Use a dummy connection string for design time
            optionsBuilder.UseMySql("Server=localhost;Database=avantipoint_packages;", 
                new MySqlServerVersion(new Version(8, 0, 21)));

            return new MySqlContext(optionsBuilder.Options);
        }
    }
}

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
            optionsBuilder.UseMySQL("Server=localhost;Database=avantipoint_packages;User=root;Password=password;");
            return new MySqlContext(optionsBuilder.Options);
        }
    }
}

using System.Linq;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySql.Data.MySqlClient;

namespace AvantiPoint.Packages.Database.MySql
{
    public class MySqlContext : AbstractContext
    {
        /// <summary>
        /// The MySQL Server error code for when a unique constraint is violated.
        /// </summary>
        private const int UniqueConstraintViolationErrorCode = 1062;

        public MySqlContext(DbContextOptions<MySqlContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Oracle's MySQL provider maps long strings to varchar, which can exceed InnoDB row size limits.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType != typeof(string)
                        || property.GetMaxLength() is not >= 1024
                        || property.GetDefaultValue() is not null)
                    {
                        continue;
                    }

                    if (property.GetContainingIndexes().Any())
                    {
                        // MySQL limits indexed utf8mb4 string keys to 768 characters.
                        property.SetMaxLength(768);
                        property.SetColumnType("varchar(768)");
                        continue;
                    }

                    property.SetColumnType("longtext");
                }
            }
        }

        public override bool IsUniqueConstraintViolationException(DbUpdateException exception)
        {
            return exception.InnerException is MySqlException mysqlException &&
                   mysqlException.Number == UniqueConstraintViolationErrorCode;
        }

        /// <summary>
        /// MySQL does not support LIMIT clauses in subqueries for certain subquery operators.
        /// See: https://dev.mysql.com/doc/refman/8.0/en/subquery-restrictions.html
        /// </summary>
        public override bool SupportsLimitInSubqueries => false;
    }
}

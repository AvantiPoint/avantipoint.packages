using System.Linq;
using AvantiPoint.Packages.Core;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.Database.SqlServer
{
    public class SqlServerContext : AbstractContext
    {
        /// <summary>
        /// SQL Server reports unique indexes separately from unique constraints.
        /// </summary>
        private const int UniqueConstraintViolationErrorCode = 2627;
        private const int UniqueIndexViolationErrorCode = 2601;

        public SqlServerContext(DbContextOptions<SqlServerContext> options)
            : base(options)
        { }

        /// <summary>
        /// Check whether a <see cref="DbUpdateException"/> is due to a SQL unique constraint violation.
        /// </summary>
        /// <param name="exception">The exception to inspect.</param>
        /// <returns>Whether the exception was caused to SQL unique constraint violation.</returns>
        public override bool IsUniqueConstraintViolationException(DbUpdateException exception)
        {
            if (exception.GetBaseException() is SqlException sqlException)
            {
                return sqlException.Errors
                    .OfType<SqlError>()
                    .Any(error => error.Number is UniqueConstraintViolationErrorCode or UniqueIndexViolationErrorCode);
            }

            return false;
        }
    }
}

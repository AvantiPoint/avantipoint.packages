#nullable enable

using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AvantiPoint.Packages.Database.PostgreSql;

public class PostgreSqlContext(DbContextOptions<PostgreSqlContext> options) : AbstractContext(options)
{
    /// <summary>
    /// The PostgreSQL error code for when a unique constraint is violated.
    /// </summary>
    private const string UniqueConstraintViolationErrorCode = "23505";

    public override bool IsUniqueConstraintViolationException(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException &&
               postgresException.SqlState == UniqueConstraintViolationErrorCode;
    }
}


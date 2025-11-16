using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AvantiPoint.Packages.Core
{
    public interface IContext : IDisposable
    {
        DatabaseFacade Database { get; }

        DbSet<Package> Packages { get; set; }

        DbSet<PackageDownload> PackageDownloads { get; set; }

        /// <summary>
        /// Check whether a <see cref="DbUpdateException"/> is due to a SQL unique constraint violation.
        /// </summary>
        /// <param name="exception">The exception to inspect.</param>
        /// <returns>Whether the exception was caused to SQL unique constraint violation.</returns>
        bool IsUniqueConstraintViolationException(DbUpdateException exception);

        /// <summary>
        /// Whether this database engine supports LINQ "Take" in subqueries.
        /// </summary>
        bool SupportsLimitInSubqueries { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Applies any pending migrations for the context to the database.
        /// Creates the database if it does not already exist.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the task.</param>
        /// <returns>A task that completes once migrations are applied.</returns>
        Task RunMigrationsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Finds packages by ID with optimized query strategy.
        /// Each provider implements this method using its most efficient approach:
        /// - Relational databases use views with JSON aggregation
        /// - Non-relational providers use their native query capabilities
        /// </summary>
        /// <param name="id">The package identifier.</param>
        /// <param name="includeUnlisted">Whether to include unlisted packages.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A read-only list of packages matching the identifier.</returns>
        Task<IReadOnlyList<Package>> FindPackagesAsync(
            string id,
            bool includeUnlisted,
            CancellationToken cancellationToken);
    }
}

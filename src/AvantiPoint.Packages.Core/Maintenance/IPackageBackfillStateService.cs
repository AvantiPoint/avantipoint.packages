using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Service for managing package metadata backfill state.
    /// </summary>
    public interface IPackageBackfillStateService
    {
        /// <summary>
        /// Gets the current backfill state from storage.
        /// </summary>
        /// <param name="cancellationToken">A token to cancel the task.</param>
        /// <returns>The current state, or a new state if none exists.</returns>
        Task<PackageBackfillState> GetStateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the backfill state to storage.
        /// </summary>
        /// <param name="state">The state to save.</param>
        /// <param name="cancellationToken">A token to cancel the task.</param>
        Task SaveStateAsync(PackageBackfillState state, CancellationToken cancellationToken = default);
    }
}

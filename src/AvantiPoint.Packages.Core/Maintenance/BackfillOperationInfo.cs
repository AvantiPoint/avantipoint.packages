using System;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Information about a specific backfill operation.
    /// </summary>
    public class BackfillOperationInfo
    {
        /// <summary>
        /// Gets or sets when the operation was last run.
        /// </summary>
        public DateTimeOffset? LastRunTime { get; set; }

        /// <summary>
        /// Gets or sets when the operation completed successfully.
        /// </summary>
        public DateTimeOffset? CompletedTime { get; set; }

        /// <summary>
        /// Gets or sets the number of packages processed.
        /// </summary>
        public int PackagesProcessed { get; set; }

        /// <summary>
        /// Gets or sets the number of packages updated.
        /// </summary>
        public int PackagesUpdated { get; set; }

        /// <summary>
        /// Gets or sets whether the operation completed successfully.
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// Gets or sets any error message from the last run.
        /// </summary>
        public string LastError { get; set; }
    }
}

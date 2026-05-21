using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core.Maintenance
{
    /// <summary>
    /// Represents the state of package metadata backfill operations.
    /// This state is persisted to storage to track which backfill operations have been completed.
    /// </summary>
    public class PackageBackfillState
    {
        /// <summary>
        /// Gets or sets the version of the state file format.
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// Gets or sets the dictionary of completed backfill operations.
        /// Key is the operation name, value is the completion timestamp.
        /// </summary>
        public Dictionary<string, DateTimeOffset> CompletedOperations { get; set; } = new Dictionary<string, DateTimeOffset>();

        /// <summary>
        /// Gets or sets information about the last repository commit backfill operation.
        /// </summary>
        public BackfillOperationInfo RepositoryCommitBackfill { get; set; }
    }
}

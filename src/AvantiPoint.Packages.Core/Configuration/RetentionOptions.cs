using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// Retention policy for published package versions, applied periodically by
    /// <see cref="Maintenance.RetentionHostedService"/>. Only <b>prerelease</b> versions of
    /// <b>published</b> (non-mirrored) packages are ever pruned — stable releases and
    /// upstream-cached packages are never touched.
    /// </summary>
    public class RetentionOptions
    {
        public bool Enabled { get; set; }

        /// <summary>
        /// When set, keeps only the newest N prerelease versions of each package.
        /// </summary>
        public int? MaxPrereleaseVersionsPerPackage { get; set; }

        /// <summary>
        /// When set, prunes prerelease versions published longer than this many days ago.
        /// </summary>
        public int? MaxPrereleaseAgeDays { get; set; }

        /// <summary>
        /// Package IDs that are never pruned (case-insensitive).
        /// </summary>
        public List<string> ExcludedPackageIds { get; set; } = new();

        /// <summary>
        /// When true, candidates are logged but nothing is deleted.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>
        /// How often the retention scan runs. Default: 24 hours.
        /// </summary>
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(24);
    }
}

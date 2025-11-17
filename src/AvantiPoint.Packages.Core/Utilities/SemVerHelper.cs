using System.Linq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core.Utilities
{
    /// <summary>
    /// Helper utilities for SemVer2 detection and handling.
    /// Based on: https://github.com/NuGet/Home/wiki/SemVer2-support-for-nuget.org-(server-side)
    /// </summary>
    public static class SemVerHelper
    {
        /// <summary>
        /// Determines if a NuGet version is SemVer 2.0.0 specific.
        /// A version is SemVer2 if it has dot-separated prerelease labels or build metadata.
        /// </summary>
        /// <param name="version">The version to check.</param>
        /// <returns>True if the version is SemVer 2.0.0 specific.</returns>
        public static bool IsSemVer2(NuGetVersion version)
        {
            return version?.IsSemVer2 ?? false;
        }

        /// <summary>
        /// Determines if a version range contains SemVer 2.0.0 specific versions.
        /// </summary>
        /// <param name="range">The version range to check.</param>
        /// <returns>True if the range contains SemVer 2.0.0 versions.</returns>
        public static bool IsSemVer2(VersionRange range)
        {
            if (range == null)
            {
                return false;
            }

            return (range.MinVersion != null && range.MinVersion.IsSemVer2)
                || (range.MaxVersion != null && range.MaxVersion.IsSemVer2);
        }

        /// <summary>
        /// Determines if a package is SemVer 2.0.0 specific based on its version and dependencies.
        /// A package is considered SemVer2 if:
        /// - Its version is SemVer2, OR
        /// - Any of its dependency version ranges is SemVer2
        /// </summary>
        /// <param name="package">The package to check.</param>
        /// <returns>True if the package is SemVer 2.0.0 specific.</returns>
        public static bool IsSemVer2(Package package)
        {
            if (package == null)
            {
                return false;
            }

            // Check if version is SemVer2
            if (IsSemVer2(package.Version))
            {
                return true;
            }

            // Check if any dependency has a SemVer2 version range
            if (package.Dependencies != null)
            {
                foreach (var dependency in package.Dependencies)
                {
                    if (!string.IsNullOrEmpty(dependency.VersionRange))
                    {
                        if (VersionRange.TryParse(dependency.VersionRange, out var range))
                        {
                            if (IsSemVer2(range))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the SemVerLevel for a package based on detection.
        /// </summary>
        /// <param name="package">The package to check.</param>
        /// <returns>The detected SemVerLevel.</returns>
        public static SemVerLevel GetSemVerLevel(Package package)
        {
            return IsSemVer2(package) ? SemVerLevel.SemVer2 : SemVerLevel.Unknown;
        }
    }
}

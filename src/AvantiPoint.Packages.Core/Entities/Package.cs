using System;
using System.Collections.Generic;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    // See NuGetGallery's: https://github.com/NuGet/NuGetGallery/blob/master/src/NuGetGallery.Core/Entities/Package.cs
    public class Package
    {
        public int Key { get; set; }

        public string Id { get; set; }

        public NuGetVersion Version
        {
            get
            {
                // Favor the original version string as it contains more information.
                // Packages uploaded with older versions of AvantiPoint Packages may not have the original version string.
                return NuGetVersion.Parse(
                    OriginalVersionString != null
                        ? OriginalVersionString
                        : NormalizedVersionString);
            }

            set
            {
                NormalizedVersionString = value.ToNormalizedString().ToLowerInvariant();
                OriginalVersionString = value.OriginalVersion;
            }
        }

        public string[] Authors { get; set; }
        public string Description { get; set; }

        [Obsolete("Use PackageDownloads")]
        public long Downloads { get; set; }
        public bool HasReadme { get; set; }
        public bool HasEmbeddedIcon { get; set; }
        public bool HasEmbeddedLicense { get; set; }
        public bool IsPrerelease { get; set; }
        public string ReleaseNotes { get; set; }
        public string Language { get; set; }
        public bool Listed { get; set; }
        public string LicenseExpression { get; set; }
        public bool IsSigned { get; set; }
        public bool IsTool { get; set; }
        public bool IsDevelopmentDependency { get; set; }
        public string MinClientVersion { get; set; }
        public DateTime Published { get; set; }
        public bool RequireLicenseAcceptance { get; set; }
        public SemVerLevel SemVerLevel { get; set; }
        public string Summary { get; set; }
        public string Title { get; set; }

        public Uri IconUrl { get; set; }
        public Uri LicenseUrl { get; set; }
        public Uri ProjectUrl { get; set; }

        public Uri RepositoryUrl { get; set; }
        public string RepositoryType { get; set; }
        public string RepositoryCommit { get; set; }
        public DateTime? RepositoryCommitDate { get; set; }

        public string[] Tags { get; set; }

        // Deprecation properties
        public bool IsDeprecated { get; set; }
        public string[] DeprecationReasons { get; set; }
        public string DeprecationMessage { get; set; }
        public string DeprecatedAlternatePackageId { get; set; }
        public string DeprecatedAlternatePackageVersionRange { get; set; }

        /// <summary>
        /// Used for optimistic concurrency.
        /// </summary>
        public byte[] RowVersion { get; set; }

        public List<PackageDependency> Dependencies { get; set; }
        public List<PackageType> PackageTypes { get; set; }
        public List<TargetFramework> TargetFrameworks { get; set; }
        public virtual ICollection<PackageDownload> PackageDownloads { get; set; }

        // JSON columns for optimized queries - populated via database triggers/computed columns
        public string DependenciesJson { get; set; }
        public string PackageTypesJson { get; set; }
        public string TargetFrameworksJson { get; set; }

        public string NormalizedVersionString { get; set; }
        public string OriginalVersionString { get; set; }


        public string IconUrlString => IconUrl?.AbsoluteUri ?? string.Empty;
        public string LicenseUrlString => LicenseUrl?.AbsoluteUri ?? string.Empty;
        public string ProjectUrlString => ProjectUrl?.AbsoluteUri ?? string.Empty;
        public string RepositoryUrlString => RepositoryUrl?.AbsoluteUri ?? string.Empty;
    }
}

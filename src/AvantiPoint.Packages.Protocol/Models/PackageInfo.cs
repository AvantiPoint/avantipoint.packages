using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Models
{
    public record PackageInfo
    {
        [JsonPropertyName("packageId")]
        public string PackageId { get; init; }

        [JsonPropertyName("authors")]
        public IEnumerable<string> Authors { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; }

        [JsonPropertyName("downloads")]
        public long Downloads { get; init; }

        [JsonPropertyName("hasReadme")]
        public bool HasReadme { get; init; }

        [JsonPropertyName("isListed")]
        public bool IsListed { get; init; }

        [JsonPropertyName("isDeprecated")]
        public bool IsDeprecated { get; init; }

        [JsonPropertyName("deprecationReasons")]
        public IReadOnlyList<string> DeprecationReasons { get; init; } = Array.Empty<string>();

        [JsonPropertyName("deprecationMessage")]
        public string DeprecationMessage { get; init; }

        [JsonPropertyName("deprecatedAlternatePackageId")]
        public string DeprecatedAlternatePackageId { get; init; }

        [JsonPropertyName("deprecatedAlternatePackageVersionRange")]
        public string DeprecatedAlternatePackageVersionRange { get; init; }

        [JsonPropertyName("published")]
        public DateTimeOffset Published { get; init; }

        [JsonPropertyName("summary")]
        public string Summary { get; init; }

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; init; }

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; init; }

        [JsonPropertyName("licenseExpression")]
        public string LicenseExpression { get; init; }

        [JsonPropertyName("hasEmbeddedLicense")]
        public bool HasEmbeddedLicense { get; init; }

        [JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; init; }

        [JsonPropertyName("repositoryUrl")]
        public string RepositoryUrl { get; init; }

        [JsonPropertyName("repositoryType")]
        public string RepositoryType { get; init; }

        [JsonPropertyName("tags")]
        public IEnumerable<string> Tags { get; init; }

        [JsonPropertyName("version")]
        public NuGetVersion Version { get; init; }

        [JsonPropertyName("isPrerelease")]
        public bool IsPrerelease { get; init; }

        [JsonPropertyName("isTool")]
        public bool IsTool { get; init; }

        [JsonPropertyName("isTemplate")]
        public bool IsTemplate { get; init; }

        [JsonPropertyName("isDevelopmentDependency")]
        public bool IsDevelopmentDependency { get; init; }

        [JsonPropertyName("requireLicenseAcceptance")]
        public bool RequireLicenseAcceptance { get; init; }

        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; init; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, IEnumerable<PackageDependencyInfo>> Dependencies { get; init; }

        [JsonPropertyName("targetFrameworks")]
        public IReadOnlyList<string> TargetFrameworks { get; init; } = Array.Empty<string>();

        /// <summary>
        /// How this version entered the feed: Published, Mirrored, or Cached.
        /// Kept as a string so the protocol contract does not depend on the persistence model.
        /// </summary>
        [JsonPropertyName("origin")]
        public string Origin { get; init; } = "Published";

        /// <summary>
        /// Friendly name of the configured upstream that supplied this version, when known.
        /// </summary>
        [JsonPropertyName("packageSourceName")]
        public string PackageSourceName { get; init; }

        /// <summary>
        /// Most recent successful operation recorded for the supplying upstream. This is
        /// source-level health information, not a claim that this exact package was checked.
        /// </summary>
        [JsonPropertyName("packageSourceLastSyncSuccessAt")]
        public DateTimeOffset? PackageSourceLastSyncSuccessAt { get; init; }

        /// <summary>
        /// When this version has not been stored locally, records when its metadata was read
        /// directly from the configured upstream for this response.
        /// </summary>
        [JsonPropertyName("metadataFetchedAt")]
        public DateTimeOffset? MetadataFetchedAt { get; init; }

        /// <summary>
        /// The configured behavior that will apply if this live upstream version is downloaded.
        /// Null for versions that are already represented by local feed metadata.
        /// </summary>
        [JsonPropertyName("upstreamCachingStrategy")]
        public string UpstreamCachingStrategy { get; init; }
    }
}

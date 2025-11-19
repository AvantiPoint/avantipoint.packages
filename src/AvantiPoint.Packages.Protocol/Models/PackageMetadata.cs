using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AvantiPoint.Packages.Protocol.Models
{
    /// <summary>
    /// A package's metadata.
    /// 
    /// See https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource#catalog-entry
    /// </summary>
    public class PackageMetadata
    {
        /// <summary>
        /// The URL to the document used to produce this object.
        /// </summary>
        [JsonPropertyName("@id")]
        public string CatalogLeafUrl { get; set; }

        /// <summary>
        /// The catalog commit identifier for this entry.
        /// </summary>
        [JsonPropertyName("catalog:commitId")]
        public string CatalogCommitId { get; set; }

        /// <summary>
        /// The catalog commit timestamp for this entry.
        /// </summary>
        [JsonPropertyName("catalog:commitTimeStamp")]
        public DateTimeOffset? CatalogCommitTimestamp { get; set; }

        /// <summary>
        /// The ID of the package.
        /// </summary>
        [JsonPropertyName("id")]
        public string PackageId { get; set; }

        /// <summary>
        /// The full NuGet version after normalization, including any SemVer 2.0.0 build metadata.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// The original version string prior to normalization.
        /// </summary>
        [JsonPropertyName("verbatimVersion")]
        public string VerbatimVersion { get; set; }

        /// <summary>
        /// Indicates whether this catalog entry represents a prerelease package.
        /// </summary>
        [JsonPropertyName("isPrerelease")]
        public bool? IsPrerelease { get; set; }

        /// <summary>
        /// The package's authors.
        /// </summary>
        [JsonPropertyName("authors")]
        public string Authors { get; set; }

        /// <summary>
        /// The dependencies of the package, grouped by target framework.
        /// </summary>
        [JsonPropertyName("dependencyGroups")]
        public IReadOnlyList<DependencyGroupItem> DependencyGroups { get; set; }

        /// <summary>
        /// The deprecation associated with the package, if any.
        /// </summary>
        [JsonPropertyName("deprecation")]
        public PackageDeprecation Deprecation { get; set; }

        /// <summary>
        /// The package's description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The copyright statement supplied by the package authors.
        /// </summary>
        [JsonPropertyName("copyright")]
        public string Copyright { get; set; }

        /// <summary>
        /// Human-readable release notes for this version.
        /// </summary>
        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// The URL to the package's icon.
        /// </summary>
        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; set; }

        /// <summary>
        /// The relative path to the embedded icon file inside the package, if present.
        /// </summary>
        [JsonPropertyName("iconFile")]
        public string IconFile { get; set; }

        /// <summary>
        /// The package's language.
        /// </summary>
        [JsonPropertyName("language")]
        public string Language { get; set; }

        /// <summary>
        /// The URL to the package's license.
        /// </summary>
        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; set; }

        /// <summary>
        /// The SPDX license expression declared by the package, if any.
        /// </summary>
        [JsonPropertyName("licenseExpression")]
        public string LicenseExpression { get; set; }

        /// <summary>
        /// The relative path to the embedded license file.
        /// </summary>
        [JsonPropertyName("licenseFile")]
        public string LicenseFile { get; set; }

        /// <summary>
        /// Whether the package is listed in search results.
        /// If <see langword="null"/>, the package should be considered as listed.
        /// </summary>
        [JsonPropertyName("listed")]
        public bool? Listed { get; set; }

        /// <summary>
        /// The minimum NuGet client version needed to use this package.
        /// </summary>
        [JsonPropertyName("minClientVersion")]
        public string MinClientVersion { get; set; }

        /// <summary>
        /// The URL to download the package's content.
        /// </summary>
        [JsonPropertyName("packageContent")]
        public string PackageContentUrl { get; set; }

        /// <summary>
        /// The package's hash in base64.
        /// </summary>
        [JsonPropertyName("packageHash")]
        public string PackageHash { get; set; }

        /// <summary>
        /// The algorithm used to compute <see cref="PackageHash"/>.
        /// </summary>
        [JsonPropertyName("packageHashAlgorithm")]
        public string PackageHashAlgorithm { get; set; }

        /// <summary>
        /// The package size in bytes.
        /// </summary>
        [JsonPropertyName("packageSize")]
        public long? PackageSize { get; set; }

        /// <summary>
        /// The URL for the package's home page.
        /// </summary>
        [JsonPropertyName("projectUrl")]
        public string ProjectUrl { get; set; }

        /// <summary>
        /// The package's publish date.
        /// </summary>
        [JsonPropertyName("published")]
        public DateTimeOffset Published { get; set; }

        /// <summary>
        /// The timestamp when the package was created in the catalog.
        /// </summary>
        [JsonPropertyName("created")]
        public DateTimeOffset? Created { get; set; }

        /// <summary>
        /// The timestamp when the catalog entry was last edited.
        /// </summary>
        [JsonPropertyName("lastEdited")]
        public DateTimeOffset? LastEdited { get; set; }

        /// <summary>
        /// If true, the package requires its license to be accepted.
        /// </summary>
        [JsonPropertyName("requireLicenseAcceptance")]
        public bool RequireLicenseAcceptance { get; set; }

        /// <summary>
        /// The package's summary.
        /// </summary>
        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        /// <summary>
        /// The package's tags.
        /// </summary>
        [JsonPropertyName("tags")]
        public IReadOnlyList<string> Tags { get; set; }

        /// <summary>
        /// The package's title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; }

        /// <summary>
        /// Any package types declared by the author.
        /// </summary>
        [JsonPropertyName("packageTypes")]
        public IReadOnlyList<SearchResultPackageType> PackageTypes { get; set; }

        /// <summary>
        /// Frameworks the package explicitly supports.
        /// </summary>
        [JsonPropertyName("supportedFrameworks")]
        public IReadOnlyList<string> SupportedFrameworks { get; set; }

        /// <summary>
        /// Entries describing the files embedded in the package.
        /// </summary>
        [JsonPropertyName("packageEntries")]
        public IReadOnlyList<PackageEntry> PackageEntries { get; set; }

        /// <summary>
        /// Known vulnerabilities that affect this package version.
        /// </summary>
        [JsonPropertyName("vulnerabilities")]
        public IReadOnlyList<PackageVulnerabilityInfo> Vulnerabilities { get; set; }

        /// <summary>
        /// The URL for the rendered (HTML web page) view of the package README.
        /// This is an optional field that should only be included if the package has a README.
        /// </summary>
        [JsonPropertyName("readmeUrl")]
        public string ReadmeUrl { get; set; }

        /// <summary>
        /// The relative path to the packaged README file.
        /// </summary>
        [JsonPropertyName("readmeFile")]
        public string ReadmeFile { get; set; }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// AvantiPoint Packages's extensions to the package metadata model. These additions
    /// are not part of the official protocol.
    /// </summary>
    /// <remarks>
    /// This is a standalone class (not inheriting from PackageMetadata) to avoid property hiding
    /// and provide a cleaner API. It includes all properties needed for the registration API response.
    /// </remarks>
    public class NuGetApiMetadata
    {
        [JsonPropertyName("id")]
        public string PackageId { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("authors")]
        public string Authors { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("iconUrl")]
        public string? IconUrl { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("licenseUrl")]
        public string? LicenseUrl { get; set; }

        [JsonPropertyName("listed")]
        public bool? Listed { get; set; }

        [JsonPropertyName("minClientVersion")]
        public string? MinClientVersion { get; set; }

        [JsonPropertyName("releaseNotes")]
        public string? ReleaseNotes { get; set; }

        [JsonPropertyName("packageContent")]
        public string PackageContentUrl { get; set; } = string.Empty;

        [JsonPropertyName("packageTypes")]
        public IReadOnlyList<string> PackageTypes { get; set; } = Array.Empty<string>();

        [JsonPropertyName("projectUrl")]
        public string? ProjectUrl { get; set; }

        [JsonPropertyName("published")]
        public DateTimeOffset Published { get; set; }

        [JsonPropertyName("requireLicenseAcceptance")]
        public bool RequireLicenseAcceptance { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("tags")]
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("dependencyGroups")]
        public IReadOnlyList<DependencyGroupItem> DependencyGroups { get; set; } = Array.Empty<DependencyGroupItem>();

        [JsonPropertyName("deprecation")]
        public PackageDeprecation? Deprecation { get; set; }

        [JsonPropertyName("readmeUrl")]
        public string? ReadmeUrl { get; set; }

        // AvantiPoint Packages extensions (not part of official protocol)
        [JsonPropertyName("downloads")]
        public long Downloads { get; set; }

        [JsonPropertyName("hasReadme")]
        public bool HasReadme { get; set; }

        [JsonPropertyName("repositoryUrl")]
        public string? RepositoryUrl { get; set; }

        [JsonPropertyName("repositoryType")]
        public string? RepositoryType { get; set; }

        [JsonPropertyName("repositoryCommit")]
        public string? RepositoryCommit { get; set; }

        [JsonPropertyName("repositoryCommitDate")]
        public DateTimeOffset? RepositoryCommitDate { get; set; }
    }
}

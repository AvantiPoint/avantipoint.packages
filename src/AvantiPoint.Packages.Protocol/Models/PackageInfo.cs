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

        [JsonPropertyName("published")]
        public DateTimeOffset Published { get; init; }

        [JsonPropertyName("summary")]
        public string Summary { get; init; }

        [JsonPropertyName("iconUrl")]
        public string IconUrl { get; init; }

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl { get; init; }

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

        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; init; }

        [JsonPropertyName("dependencies")]
        public Dictionary<string, IEnumerable<PackageDependencyInfo>> Dependencies { get; init; }
    }
}

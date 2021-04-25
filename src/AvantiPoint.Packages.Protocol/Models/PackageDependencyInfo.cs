using System.Text.Json.Serialization;

namespace AvantiPoint.Packages.Protocol.Models
{
    public record PackageDependencyInfo
    {
        [JsonPropertyName("packageId")]
        public string PackageId { get; init; }

        [JsonPropertyName("versionRange")]
        public string VersionRange { get; init; }

        [JsonPropertyName("isLocalDependency")]
        public bool IsLocalDependency { get; init; }
    }
}

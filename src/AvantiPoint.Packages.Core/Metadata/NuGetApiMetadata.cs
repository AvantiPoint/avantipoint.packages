using System.Collections.Generic;
using System.Text.Json.Serialization;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// AvantiPoint Packages's extensions to the package metadata model. These additions
    /// are not part of the official protocol.
    /// </summary>
    public class NuGetApiMetadata : PackageMetadata
    {
        [JsonPropertyName("downloads")]
        public long Downloads { get; set; }

        [JsonPropertyName("hasReadme")]
        public bool HasReadme { get; set; }

        [JsonPropertyName("packageTypes")]
        public IReadOnlyList<string> PackageTypes { get; set; }

        /// <summary>
        /// The package's release notes.
        /// </summary>
        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; set; }

        [JsonPropertyName("repositoryUrl")]
        public string RepositoryUrl { get; set; }

        [JsonPropertyName("repositoryType")]
        public string RepositoryType { get; set; }
    }
}

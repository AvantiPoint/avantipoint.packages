using System.Text.Json.Serialization;

namespace AvantiPoint.Packages.Protocol.Models
{
    /// <summary>
    /// Represents a single file entry exposed by a catalog package entry.
    /// </summary>
    public class PackageEntry
    {
        [JsonPropertyName("@id")]
        public string EntryUrl { get; set; }

        [JsonPropertyName("@type")]
        public string EntryType { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("length")]
        public long? Length { get; set; }

        [JsonPropertyName("compressedLength")]
        public long? CompressedLength { get; set; }
    }
}

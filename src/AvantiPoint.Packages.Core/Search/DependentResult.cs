using System.Text.Json.Serialization;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// A package that depends on the queried package.
    /// </summary>
    public class DependentResult
    {
        /// <summary>
        /// The dependent package id.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// The description of the dependent package.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// The total downloads for the dependent package.
        /// </summary>
        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads { get; set; }
    }
}

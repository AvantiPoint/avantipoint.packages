using System.Collections.Generic;
using System.Text.Json.Serialization;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// AvantiPoint Packages's extensions to a registration index response.
    /// Extends <see cref="RegistrationIndexResponse"/>.
    /// </summary>
    /// <remarks>
    /// TODO: After this project is updated to .NET 5, make <see cref="AvantiPoint.PackagesRegistrationIndexResponse"/>
    /// extend <see cref="RegistrationIndexResponse"/> and remove identical properties.
    /// Properties that are modified should be marked with the "new" modified.
    /// See: https://github.com/dotnet/runtime/pull/32107
    /// </remarks>
    public class NuGetApiRegistrationIndexResponse
    {
#region Original properties from RegistrationIndexResponse.
        [JsonPropertyName("@id")]
        public string RegistrationIndexUrl { get; set; }

        [JsonPropertyName("@type")]
        public IReadOnlyList<string> Type { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
#endregion

        /// <summary>
        /// The pages that contain all of the versions of the package, ordered
        /// by the package's version. This was modified to use AvantiPoint Packages's extended
        /// registration index page model.
        /// </summary>
        [JsonPropertyName("items")]
        public IReadOnlyList<NuGetApiRegistrationIndexPage> Pages { get; set; }

        /// <summary>
        /// The package's total downloads across all versions.
        /// This is not part of the official NuGet protocol.
        /// </summary>
        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads { get; set; }
    }
}

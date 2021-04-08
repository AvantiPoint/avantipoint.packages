using System.Collections.Generic;
using System.Linq;
using AvantiPoint.Packages.Protocol.Models;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol
{
    /// <summary>
    /// These are documented interpretations of values returned by the Package Content resource.
    /// </summary>
    public static class PackageContentModelExtensions
    {
        /// <summary>
        /// Parse the package versions as <see cref="NuGetVersion" />s.
        /// </summary>
        /// <param name="response">The package versions response.</param>
        /// <returns>The package versions.</returns>
        public static IReadOnlyList<NuGetVersion> ParseVersions(this PackageVersionsResponse response)
        {
            return response
                .Versions
                .Select(NuGetVersion.Parse)
                .ToList();
        }
    }
}

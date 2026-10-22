using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The NuGet V3 search request.
    /// See: https://docs.microsoft.com/en-us/nuget/api/search-query-service-resource#request-parameters
    /// </summary>
    public class SearchRequest
    {
        /// <summary>
        /// The number of results to skip, for pagination.
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// The number of results to return, for pagination.
        /// </summary>
        public int Take { get; set; }

        /// <summary>
        /// Whether to include pre-release packages.
        /// </summary>
        public bool IncludePrerelease { get; set; }

        /// <summary>
        /// Whether to include SemVer 2.0.0 compatible packages.
        /// </summary>
        public bool IncludeSemVer2 { get; set; }

        /// <summary>
        /// Filter results to a package type. If null, no filter is applied.
        /// </summary>
        private string _packageType;
        public string PackageType
        {
            get => _packageType;
            set => _packageType = NormalizePackageType(value);
        }

        internal static string NormalizePackageType(string value) => value?.Trim().ToLowerInvariant() switch
        {
            "dependency" => "Dependency",
            "dotnettool" => "DotnetTool",
            "template" => "Template",
            "mcpserver" => "McpServer",
            "" => null,
            _ => value?.Trim(),
        };

        /// <summary>
        /// Filters results to a target framework. If null, no filter is applied.
        /// </summary>
        public string Framework { get; set; }

        /// <summary>
        /// Filters results to packages matching the selected target frameworks. When populated,
        /// this takes precedence over the legacy single <see cref="Framework"/> value.
        /// </summary>
        public IReadOnlyList<string> Frameworks { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Whether a selected target framework should also match package assets compatible with it.
        /// </summary>
        public bool IncludeCompatibleFrameworks { get; set; } = true;

        /// <summary>
        /// Determines whether a package must match any or all selected target frameworks.
        /// </summary>
        public FrameworkFilterMode FrameworkFilterMode { get; set; } = FrameworkFilterMode.Any;

        /// <summary>
        /// The search query.
        /// </summary>
        public string Query { get; set; }
    }
}

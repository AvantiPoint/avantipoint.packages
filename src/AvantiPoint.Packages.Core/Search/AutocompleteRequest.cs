namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The NuGet V3 search request.
    /// See: https://docs.microsoft.com/en-us/nuget/api/search-autocomplete-service-resource#request-parameters
    /// </summary>
    public class AutocompleteRequest
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
            set => _packageType = SearchRequest.NormalizePackageType(value);
        }

        /// <summary>
        /// The search query.
        /// </summary>
        public string Query { get; set; }
    }
}

using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Service for searching NuGet packages from a configured feed.
/// This abstraction allows the component to work with any NuGet feed
/// (authenticated or not, same-site or cross-origin).
/// </summary>
public interface INuGetSearchService
{
    /// <summary>
    /// Search for packages matching the specified criteria.
    /// </summary>
    /// <param name="query">The search query. If null or empty, returns all packages.</param>
    /// <param name="skip">Number of results to skip (for pagination).</param>
    /// <param name="take">Number of results to return (page size).</param>
    /// <param name="includePrerelease">Whether to include prerelease packages.</param>
    /// <param name="packageType">Filter to specific package type (e.g., "Dependency").</param>
    /// <param name="framework">Filter to specific target framework (e.g., "net10.0").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search response containing matching packages.</returns>
    Task<SearchResponse> SearchAsync(
        string? query = null,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        string? packageType = null,
        string? framework = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search using the complete gallery filter model, including multiple target frameworks.
    /// Implementations should override this method when they support the extended feed query.
    /// </summary>
    Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchAsync(
            request.Query,
            request.Skip,
            request.Take,
            request.IncludePrerelease,
            request.PackageType,
            request.Frameworks?.FirstOrDefault() ?? request.Framework,
            cancellationToken);

    /// <summary>
    /// Autocomplete package IDs matching the query via Host Autocomplete
    /// (ISearchService.AutocompleteAsync). Empty query returns no suggestions.
    /// </summary>
    Task<IReadOnlyList<string>> AutocompleteAsync(
        string? query = null,
        int skip = 0,
        int take = 10,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default);
}

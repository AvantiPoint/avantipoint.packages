using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.Host.Services;

/// <summary>
/// Host UI search against the in-process ISearchService so browse does not
/// round-trip HTTP /v3/search (AuthorizedNuGetConsumerFilter requires a feed token).
/// </summary>
public sealed class InProcessNuGetSearchService(ISearchService search) : INuGetSearchService
{
    public Task<SearchResponse> SearchAsync(
        string? query = null,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        string? packageType = null,
        string? framework = null,
        CancellationToken cancellationToken = default)
    {
        var request = new SearchRequest
        {
            Skip = skip,
            Take = take,
            IncludePrerelease = includePrerelease,
            IncludeSemVer2 = true,
            PackageType = packageType,
            Framework = framework,
            Query = query ?? string.Empty,
        };
        return search.SearchAsync(request, cancellationToken);
    }

    public Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return search.SearchAsync(request, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(
        string? query = null,
        int skip = 0,
        int take = 10,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        var request = new AutocompleteRequest
        {
            Query = query ?? string.Empty,
            Skip = skip,
            Take = take,
            IncludePrerelease = includePrerelease,
            IncludeSemVer2 = true,
        };
        var response = await search.AutocompleteAsync(request, cancellationToken);
        return response.Data ?? Array.Empty<string>();
    }
}

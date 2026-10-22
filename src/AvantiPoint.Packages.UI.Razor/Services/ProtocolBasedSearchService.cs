using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Search service implementation that uses NuGetClientFactory to auto-discover
/// the search endpoint from the service index.
/// </summary>
internal class ProtocolBasedSearchService(HttpClient httpClient, string serviceIndexUrl) : INuGetSearchService
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly string _serviceIndexUrl = serviceIndexUrl ?? throw new ArgumentNullException(nameof(serviceIndexUrl));
    private NuGetClientFactory? _clientFactory;
    private ISearchClient? _searchClient;
    private IAutocompleteClient? _autocompleteClient;

    public async Task<SearchResponse> SearchAsync(
        string? query = null,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        string? packageType = null,
        string? framework = null,
        CancellationToken cancellationToken = default)
    {
        // Lazy-initialize the client factory and search client
        if (_searchClient == null)
        {
            _clientFactory = new NuGetClientFactory(_httpClient, _serviceIndexUrl);
            _searchClient = _clientFactory.CreateSearchClient();
        }

        // Note: packageType and framework are unofficial parameters supported by this feed
        // but not part of the standard NuGet v3 protocol
        return await _searchClient.SearchAsync(
            query,
            skip,
            take,
            includePrerelease,
            includePrerelease, // semVerLevel 2.0 includes prerelease
            packageType,
            framework,
            cancellationToken);
    }

    public async Task<SearchResponse> SearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _clientFactory ??= new NuGetClientFactory(_httpClient, _serviceIndexUrl);
        _searchClient ??= _clientFactory.CreateSearchClient();
        var frameworks = FrameworkSearchFilter.GetRequestedFrameworks(request);

        return await _searchClient.SearchAsync(
            request.Query,
            request.Skip,
            request.Take,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            request.Framework ?? frameworks.FirstOrDefault(),
            cancellationToken,
            frameworks,
            request.IncludeCompatibleFrameworks,
            request.FrameworkFilterMode.ToString());
    }

    public async Task<IReadOnlyList<string>> AutocompleteAsync(
        string? query = null,
        int skip = 0,
        int take = 10,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        if (_autocompleteClient == null)
        {
            _clientFactory ??= new NuGetClientFactory(_httpClient, _serviceIndexUrl);
            _autocompleteClient = _clientFactory.CreateAutocompleteClient();
        }

        var response = await _autocompleteClient.AutocompleteAsync(
            query,
            skip,
            take,
            includePrerelease,
            includeSemVer2: true,
            cancellationToken);
        return response.Data ?? Array.Empty<string>();
    }
}

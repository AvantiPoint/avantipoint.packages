using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using AvantiPoint.Packages.Protocol.Models;

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
}

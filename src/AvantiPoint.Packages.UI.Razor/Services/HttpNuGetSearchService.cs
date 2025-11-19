//using System;
//using System.Collections.Generic;
//using System.Net.Http;
//using System.Net.Http.Headers;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using AvantiPoint.Packages.Protocol.Models;

//namespace AvantiPoint.Packages.UI.Services;

///// <summary>
///// HTTP-based implementation of INuGetSearchService that can connect to any NuGet v3 feed.
///// Supports both same-origin and cross-origin feeds, with optional authentication.
///// </summary>
///// <remarks>
///// Create a new HTTP-based search service.
///// </remarks>
///// <param name="httpClient">HTTP client for making requests.</param>
///// <param name="searchEndpoint">
///// The NuGet v3 search endpoint URL. For same-site feeds, this can be a relative path like "/v3/search".
///// For external feeds, use the full URL from the service index (e.g., "https://api.nuget.org/v3/search").
///// </param>
///// <param name="authTokenProvider">
///// Optional function to provide authentication token (API key or bearer token).
///// Called before each request to get the current token. Return null for unauthenticated requests.
///// </param>
//public class HttpNuGetSearchService(
//    HttpClient httpClient,
//    string searchEndpoint,
//    Func<Task<string?>>? authTokenProvider = null) : INuGetSearchService
//{
//    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
//    private readonly string _searchEndpoint = searchEndpoint ?? throw new ArgumentNullException(nameof(searchEndpoint));

//    /// <inheritdoc />
//    public async Task<SearchResponse> SearchAsync(
//        string? query = null,
//        int skip = 0,
//        int take = 20,
//        bool includePrerelease = false,
//        string? packageType = null,
//        string? framework = null,
//        CancellationToken cancellationToken = default)
//    {
//        var queryParams = new List<string>
//        {
//            $"skip={skip}",
//            $"take={take}",
//            $"prerelease={includePrerelease.ToString().ToLowerInvariant()}",
//            "semVerLevel=2.0.0"
//        };

//        if (!string.IsNullOrWhiteSpace(query))
//        {
//            queryParams.Add($"q={Uri.EscapeDataString(query)}");
//        }

//        if (!string.IsNullOrWhiteSpace(packageType))
//        {
//            queryParams.Add($"packageType={Uri.EscapeDataString(packageType)}");
//        }

//        if (!string.IsNullOrWhiteSpace(framework))
//        {
//            queryParams.Add($"framework={Uri.EscapeDataString(framework)}");
//        }

//        var url = $"{_searchEndpoint}?{string.Join("&", queryParams)}";

//        using var request = new HttpRequestMessage(HttpMethod.Get, url);

//        // Apply authentication if provider is configured
//        if (authTokenProvider != null)
//        {
//            var token = await authTokenProvider();
//            if (!string.IsNullOrEmpty(token))
//            {
//                // Support both API key (X-NuGet-ApiKey) and Bearer token authentication
//                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
//                {
//                    request.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
//                }
//                else
//                {
//                    request.Headers.Add("X-NuGet-ApiKey", token);
//                }
//            }
//        }

//        var response = await _httpClient.SendAsync(request, cancellationToken);
//        response.EnsureSuccessStatusCode();

//        var content = await response.Content.ReadAsStringAsync(cancellationToken);
//        return JsonSerializer.Deserialize<SearchResponse>(content, new JsonSerializerOptions
//        {
//            PropertyNameCaseInsensitive = true
//        }) ?? new SearchResponse { Data = Array.Empty<SearchResult>(), TotalHits = 0 };
//    }
//}

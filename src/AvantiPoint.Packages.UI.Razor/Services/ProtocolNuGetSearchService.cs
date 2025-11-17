using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Implementation of INuGetSearchService that uses the AvantiPoint.Packages.Protocol NuGetClient.
/// This is ideal when the component is used within the same application as the NuGet feed server.
/// </summary>
/// <remarks>
/// Create a new protocol-based search service using the NuGetClient.
/// </remarks>
/// <param name="client">The configured NuGetClient instance.</param>
public class ProtocolNuGetSearchService(NuGetClient client) : INuGetSearchService
{
    /// <inheritdoc />
    public async Task<SearchResponse> SearchAsync(
        string? query = null,
        int skip = 0,
        int take = 20,
        bool includePrerelease = false,
        string? packageType = null,
        string? framework = null,
        CancellationToken cancellationToken = default)
    {
        var results = await client.SearchAsync(
            query: query,
            skip: skip,
            take: take,
            includePrerelease: includePrerelease,
            packageType: packageType,
            framework: framework,
            cancellationToken: cancellationToken);

        // NuGetClient.SearchAsync returns IReadOnlyList<SearchResult>
        // We need to wrap it in a SearchResponse for consistency
        return new SearchResponse
        {
            Data = results,
            TotalHits = results.Count // Note: NuGetClient doesn't provide total count
        };
    }
}

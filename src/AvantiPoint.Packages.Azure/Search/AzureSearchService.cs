using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Azure.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Azure.Search.Documents;
using Microsoft.Extensions.Options;
using AzureSearchQueryOptions = Azure.Search.Documents.SearchOptions;
using CoreSearchOptions = AvantiPoint.Packages.Core.SearchOptions;

namespace AvantiPoint.Packages.Azure.Search;

public class AzureSearchService : ISearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchDocumentMapper _mapper;
    private readonly IFrameworkCompatibilityService _frameworks;
    private readonly CoreSearchOptions _searchOptions;

    public AzureSearchService(
        SearchClient searchClient,
        SearchDocumentMapper mapper,
        IFrameworkCompatibilityService frameworks,
        IOptions<AzureSearchOptions> options,
        IOptions<CoreSearchOptions> searchOptions)
    {
        _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
        _searchOptions = searchOptions?.Value ?? throw new ArgumentNullException(nameof(searchOptions));
        _ = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(request.IncludePrerelease, request.IncludeSemVer2, request.PackageType, request.Framework);
        var searchText = BuildSearchText(request.Query);

        var response = await _searchClient.SearchAsync<AzureSearchDocument>(
            searchText,
            new AzureSearchQueryOptions
            {
                Filter = filter,
                Skip = request.Skip,
                Size = request.Take,
                IncludeTotalCount = true,
            },
            cancellationToken);

        var documents = new List<PackageSearchDocument>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = AzureSearchDocumentMapper.FromAzure(result.Document);
            var filtered = SearchVisibility.FilterForRequest(document, request.IncludePrerelease, request.IncludeSemVer2);
            if (filtered != null)
            {
                documents.Add(filtered);
            }
        }

        return _mapper.MapSearch(documents, response.Value.TotalCount ?? documents.Count);
    }

    public async Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken)
    {
        var filter = BuildFilter(request.IncludePrerelease, request.IncludeSemVer2, packageType: null, framework: null);
        var searchText = string.IsNullOrWhiteSpace(request.Query) ? "*" : $"{request.Query}*";

        var response = await _searchClient.SearchAsync<AzureSearchDocument>(
            searchText,
            new AzureSearchQueryOptions
            {
                Filter = filter,
                Skip = request.Skip,
                Size = request.Take,
                IncludeTotalCount = true,
                SearchFields = { nameof(AzureSearchDocument.Id) },
            },
            cancellationToken);

        var ids = new List<string>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            ids.Add(result.Document.Id);
        }

        return new AutocompleteResponse
        {
            TotalHits = response.Value.TotalCount ?? ids.Count,
            Data = ids,
            Context = AutocompleteContext.Default,
        };
    }

    public async Task<AutocompleteResponse> ListPackageVersionsAsync(VersionsRequest request, CancellationToken cancellationToken)
    {
        var key = request.PackageId.ToLowerInvariant();
        var response = await _searchClient.GetDocumentAsync<AzureSearchDocument>(key, cancellationToken: cancellationToken);

        if (response.Value?.Versions == null)
        {
            return new AutocompleteResponse { TotalHits = 0, Data = [], Context = AutocompleteContext.Default };
        }

        var document = AzureSearchDocumentMapper.FromAzure(response.Value);
        var filtered = SearchVisibility.FilterVersions(
            document.Versions,
            document.VersionIsPrerelease,
            document.VersionIsSemVer2,
            request.IncludePrerelease,
            request.IncludeSemVer2);

        return new AutocompleteResponse
        {
            TotalHits = filtered.Count,
            Data = filtered.ToList(),
            Context = AutocompleteContext.Default,
        };
    }

    public async Task<DependentsResponse> FindDependentsAsync(string packageId, CancellationToken cancellationToken)
    {
        var originFilter = BuildOriginFilter();
        var filter = string.IsNullOrEmpty(originFilter)
            ? $"Dependencies/any(d: d eq '{packageId.ToLowerInvariant()}')"
            : $"Dependencies/any(d: d eq '{packageId.ToLowerInvariant()}') and {originFilter}";
        var response = await _searchClient.SearchAsync<AzureSearchDocument>(
            "*",
            new AzureSearchQueryOptions
            {
                Filter = filter,
                Size = 20,
                IncludeTotalCount = true,
            },
            cancellationToken);

        var data = new List<DependentResult>();
        await foreach (var result in response.Value.GetResultsAsync())
        {
            data.Add(new DependentResult
            {
                Id = result.Document.Id,
                Description = result.Document.Description,
                TotalDownloads = result.Document.TotalDownloads,
            });
        }

        return new DependentsResponse
        {
            TotalHits = response.Value.TotalCount ?? data.Count,
            Data = data,
        };
    }

    private string BuildFilter(bool includePrerelease, bool includeSemVer2, string packageType, string framework)
    {
        var profileBit = SearchVisibility.GetProfileBit(includePrerelease, includeSemVer2);
        var parts = new List<string> { $"(visibilityMask and {profileBit}) ne 0" };

        var originFilter = BuildOriginFilter();
        if (!string.IsNullOrEmpty(originFilter))
        {
            parts.Add(originFilter);
        }

        if (!string.IsNullOrWhiteSpace(packageType))
        {
            parts.Add($"PackageTypes/any(t: t eq '{packageType}')");
        }

        if (!string.IsNullOrWhiteSpace(framework))
        {
            var frameworks = _frameworks.FindAllCompatibleFrameworks(framework);
            var frameworkFilter = string.Join(" or ", frameworks.Select(f => $"Frameworks/any(x: x eq '{f}')"));
            parts.Add($"({frameworkFilter})");
        }

        return string.Join(" and ", parts);
    }

    private string BuildOriginFilter()
    {
        var allowed = PackageOriginFilter.GetAllowedOriginNames(_searchOptions);
        if (allowed.Count == 1)
        {
            return $"Origin eq '{allowed[0]}'";
        }

        var clauses = allowed.Select(o => $"Origin eq '{o}'");
        return $"({string.Join(" or ", clauses)})";
    }

    private static string BuildSearchText(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "*";
        }

        return $"{query.TrimEnd().TrimEnd('*')}*";
    }
}

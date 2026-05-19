using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace AvantiPoint.Packages.Elasticsearch;

public class ElasticsearchSearchService : ISearchService
{
    private readonly IOpenSearchClient _client;
    private readonly SearchDocumentMapper _mapper;
    private readonly IFrameworkCompatibilityService _frameworks;
    private readonly string _indexName;

    public ElasticsearchSearchService(
        IOpenSearchClient client,
        SearchDocumentMapper mapper,
        IFrameworkCompatibilityService frameworks,
        IOptions<ElasticsearchSearchOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _frameworks = frameworks ?? throw new ArgumentNullException(nameof(frameworks));
        _indexName = options.Value.IndexName;
    }

    public async Task<SearchResponse> SearchAsync(Core.SearchRequest request, CancellationToken cancellationToken)
    {
        var response = await _client.SearchAsync<ElasticsearchPackageDocument>(s => s
            .Index(_indexName)
            .From(request.Skip)
            .Size(request.Take)
            .Query(q => q.Bool(b =>
            {
                ApplyVisibilityFilter(b, request.IncludePrerelease, request.IncludeSemVer2);

                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    b.Must(m => m.Wildcard(w => w.Field(d => d.Id).Value($"*{request.Query.ToLowerInvariant()}*")));
                }

                if (!string.IsNullOrWhiteSpace(request.PackageType))
                {
                    b.Filter(f => f.Term(t => t.Field(d => d.PackageTypes).Value(request.PackageType)));
                }

                if (!string.IsNullOrWhiteSpace(request.Framework))
                {
                    var frameworks = _frameworks.FindAllCompatibleFrameworks(request.Framework);
                    b.Filter(f => f.Terms(t => t.Field(d => d.Frameworks).Terms(frameworks)));
                }

                return b;
            })), cancellationToken);

        var documents = response.Documents
            .Select(ElasticsearchDocumentMapper.FromElasticsearch)
            .Select(d => SearchVisibility.FilterForRequest(d, request.IncludePrerelease, request.IncludeSemVer2))
            .Where(d => d != null)
            .ToList()!;

        return _mapper.MapSearch(documents, response.Total);
    }

    public async Task<AutocompleteResponse> AutocompleteAsync(AutocompleteRequest request, CancellationToken cancellationToken)
    {
        var response = await _client.SearchAsync<ElasticsearchPackageDocument>(s => s
            .Index(_indexName)
            .From(request.Skip)
            .Size(request.Take)
            .Query(q => q.Bool(b =>
            {
                ApplyVisibilityFilter(b, request.IncludePrerelease, request.IncludeSemVer2);

                if (!string.IsNullOrWhiteSpace(request.Query))
                {
                    b.Must(m => m.Wildcard(w => w.Field(d => d.Id).Value($"{request.Query.ToLowerInvariant()}*")));
                }

                return b;
            })), cancellationToken);

        return new AutocompleteResponse
        {
            TotalHits = response.Total,
            Data = response.Documents.Select(d => d.Id).ToList(),
            Context = AutocompleteContext.Default,
        };
    }

    public async Task<AutocompleteResponse> ListPackageVersionsAsync(VersionsRequest request, CancellationToken cancellationToken)
    {
        var key = request.PackageId.ToLowerInvariant();
        var response = await _client.GetAsync<ElasticsearchPackageDocument>(key, g => g.Index(_indexName), cancellationToken);

        if (!response.Found || response.Source?.Versions == null)
        {
            return new AutocompleteResponse
            {
                TotalHits = 0,
                Data = [],
                Context = AutocompleteContext.Default,
            };
        }

        var document = ElasticsearchDocumentMapper.FromElasticsearch(response.Source);
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
        var response = await _client.SearchAsync<ElasticsearchPackageDocument>(s => s
            .Index(_indexName)
            .Size(20)
            .Query(q => q.Term(t => t.Field(d => d.Dependencies).Value(packageId.ToLowerInvariant()))), cancellationToken);

        var data = response.Documents.Select(d => new DependentResult
        {
            Id = d.Id,
            Description = d.Description,
            TotalDownloads = d.TotalDownloads,
        }).ToList();

        return new DependentsResponse
        {
            TotalHits = response.Total,
            Data = data,
        };
    }

    private static void ApplyVisibilityFilter(
        BoolQueryDescriptor<ElasticsearchPackageDocument> query,
        bool includePrerelease,
        bool includeSemVer2)
    {
        var profile = SearchVisibility.GetProfile(includePrerelease, includeSemVer2);
        query.Filter(f => profile switch
        {
            0 => f.Term(t => t.Field(d => d.VisibleForDefaultSearch).Value(true)),
            1 => f.Term(t => t.Field(d => d.VisibleForSemVer2Search).Value(true)),
            2 => f.Term(t => t.Field(d => d.VisibleForPrereleaseSearch).Value(true)),
            _ => f.Term(t => t.Field(d => d.VisibleForFullSearch).Value(true)),
        });
    }
}

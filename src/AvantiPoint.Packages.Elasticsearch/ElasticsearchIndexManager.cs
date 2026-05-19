using OpenSearch.Client;
using OpenSearch.Net;

namespace AvantiPoint.Packages.Elasticsearch;

internal sealed class ElasticsearchIndexManager
{
    private readonly IOpenSearchClient _client;
    private readonly string _indexName;

    public ElasticsearchIndexManager(IOpenSearchClient client, string indexName)
    {
        _client = client;
        _indexName = indexName;
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await _client.Indices.ExistsAsync(_indexName, ct: cancellationToken);
        if (exists.Exists)
        {
            return;
        }

        await _client.Indices.CreateAsync(_indexName, c => c
            .Settings(s => s.NumberOfShards(1).NumberOfReplicas(0))
            .Map<ElasticsearchPackageDocument>(m => m
                .AutoMap()
                .Properties(p => p
                    .Keyword(k => k.Name(n => n.Key))
                    .Keyword(k => k.Name(n => n.Id))
                    .Keyword(k => k.Name(n => n.SearchFilters)))), ct: cancellationToken);
    }
}

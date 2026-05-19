using AvantiPoint.Packages.Core;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace AvantiPoint.Packages.Elasticsearch;

public class ElasticsearchSearchIndexer : ISearchIndexer
{
    private readonly IOpenSearchClient _client;
    private readonly IPackageSearchDocumentFactory _documentFactory;
    private readonly ElasticsearchIndexManager _indexManager;
    private readonly string _indexName;

    public ElasticsearchSearchIndexer(
        IOpenSearchClient client,
        IPackageSearchDocumentFactory documentFactory,
        IOptions<ElasticsearchSearchOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _documentFactory = documentFactory ?? throw new ArgumentNullException(nameof(documentFactory));
        _indexName = options.Value.IndexName;
        _indexManager = new ElasticsearchIndexManager(client, _indexName);
    }

    public virtual string Key => SearchIndexerKeys.Elasticsearch;

    public async Task IndexAsync(Package package, CancellationToken cancellationToken)
    {
        await _indexManager.EnsureIndexExistsAsync(cancellationToken);

        var document = await _documentFactory.CreateAsync(package.Id, cancellationToken);
        if (document == null)
        {
            await RemoveAsync(package.Id, cancellationToken);
            return;
        }

        var esDocument = ElasticsearchDocumentMapper.ToElasticsearch(document);
        await _client.IndexAsync(esDocument, i => i
            .Index(_indexName)
            .Id(esDocument.Key), cancellationToken);
    }

    public async Task RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        var key = packageId.ToLowerInvariant();
        await _client.DeleteAsync<ElasticsearchPackageDocument>(key, d => d.Index(_indexName), cancellationToken);
    }
}

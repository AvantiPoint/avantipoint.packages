using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace AvantiPoint.Packages.Azure.Search;

internal sealed class AzureSearchIndexManager
{
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;

    public AzureSearchIndexManager(SearchIndexClient indexClient, string indexName)
    {
        _indexClient = indexClient;
        _indexName = indexName;
    }

    public async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _indexClient.GetIndexAsync(_indexName, cancellationToken);
            return;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
        }

        var index = new SearchIndex(_indexName)
        {
            Fields = new FieldBuilder().Build(typeof(AzureSearchDocument)),
        };

        await _indexClient.CreateIndexAsync(index, cancellationToken);
    }
}

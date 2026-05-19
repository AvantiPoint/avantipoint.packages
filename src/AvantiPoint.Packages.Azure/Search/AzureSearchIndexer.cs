using System;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Azure.Configuration;
using AvantiPoint.Packages.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Azure.Search;

public class AzureSearchIndexer : ISearchIndexer
{
    private readonly SearchClient _searchClient;
    private readonly IPackageSearchDocumentFactory _documentFactory;
    private readonly AzureSearchIndexManager _indexManager;

    public AzureSearchIndexer(
        SearchClient searchClient,
        IPackageSearchDocumentFactory documentFactory,
        SearchIndexClient indexClient,
        IOptions<AzureSearchOptions> options)
    {
        _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
        _documentFactory = documentFactory ?? throw new ArgumentNullException(nameof(documentFactory));
        _indexManager = new AzureSearchIndexManager(indexClient, options.Value.IndexName);
    }

    public string Key => SearchIndexerKeys.AzureSearch;

    public async Task IndexAsync(Package package, CancellationToken cancellationToken)
    {
        await _indexManager.EnsureIndexExistsAsync(cancellationToken);

        var document = await _documentFactory.CreateAsync(package.Id, cancellationToken);
        if (document == null)
        {
            await RemoveAsync(package.Id, cancellationToken);
            return;
        }

        var batch = IndexDocumentsBatch.MergeOrUpload([AzureSearchDocumentMapper.ToAzure(document)]);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    public async Task RemoveAsync(string packageId, CancellationToken cancellationToken)
    {
        var key = packageId.ToLowerInvariant();
        var batch = IndexDocumentsBatch.Delete("Key", [key]);
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }
}

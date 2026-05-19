using AvantiPoint.Packages.Aws.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Elasticsearch;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace AvantiPoint.Packages.Aws.OpenSearch;

public sealed class OpenSearchSearchIndexer : ElasticsearchSearchIndexer
{
    public OpenSearchSearchIndexer(
        IOpenSearchClient client,
        IPackageSearchDocumentFactory documentFactory,
        IOptions<OpenSearchOptions> options)
        : base(client, documentFactory, Options.Create<ElasticsearchSearchOptions>(options.Value))
    {
    }

    public override string Key => SearchIndexerKeys.OpenSearch;
}

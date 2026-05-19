using AvantiPoint.Packages.Aws.Configuration;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Elasticsearch;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace AvantiPoint.Packages.Aws.OpenSearch;

/// <summary>
/// AWS OpenSearch Service uses the same query implementation as Elasticsearch.
/// </summary>
public sealed class OpenSearchSearchService : ElasticsearchSearchService
{
    public OpenSearchSearchService(
        IOpenSearchClient client,
        SearchDocumentMapper mapper,
        IFrameworkCompatibilityService frameworks,
        IOptions<OpenSearchOptions> options)
        : base(client, mapper, frameworks, Options.Create<ElasticsearchSearchOptions>(options.Value))
    {
    }
}

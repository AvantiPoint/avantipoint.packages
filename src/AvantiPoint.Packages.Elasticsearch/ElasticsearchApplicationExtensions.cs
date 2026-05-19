using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenSearch.Client;

namespace AvantiPoint.Packages;

public static class ElasticsearchApplicationExtensions
{
    public const string SearchTypeName = "Elasticsearch";

    public static NuGetApiOptions AddElasticsearchSearch(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<ElasticsearchSearchOptions>(nameof(PackageFeedOptions.Search));
        options.Services.AddSingleton<IOpenSearchClient>(sp =>
        {
            var elasticsearchOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ElasticsearchSearchOptions>>();
            return ElasticsearchClientFactory.Create(elasticsearchOptions);
        });
        options.Services.AddTransient<ElasticsearchSearchService>();
        options.Services.AddTransient<ElasticsearchSearchIndexer>();

        options.Services.AddProvider<ISearchService>((provider, config) =>
        {
            if (!config.HasSearchType(SearchTypeName)) return null;
            return provider.GetRequiredService<ElasticsearchSearchService>();
        });

        options.Services.AddProvider<ISearchIndexer>((provider, config) =>
        {
            if (!config.HasSearchType(SearchTypeName)) return null;
            return provider.GetRequiredService<ElasticsearchSearchIndexer>();
        });

        return options;
    }

    public static NuGetApiOptions AutoDiscoverElasticsearchSearch(this NuGetApiOptions options)
        => options.AddElasticsearchSearch();
}

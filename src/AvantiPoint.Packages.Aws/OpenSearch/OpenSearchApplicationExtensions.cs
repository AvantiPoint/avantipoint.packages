using AvantiPoint.Packages.Aws.Configuration;
using AvantiPoint.Packages.Aws.OpenSearch;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Elasticsearch;
using Microsoft.Extensions.DependencyInjection;
using OpenSearch.Client;

namespace AvantiPoint.Packages;

public static class OpenSearchApplicationExtensions
{
    public const string SearchTypeName = "OpenSearch";

    public static NuGetApiOptions AddOpenSearch(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<OpenSearchOptions>(nameof(PackageFeedOptions.Search));
        options.Services.AddSingleton<IOpenSearchClient>(sp =>
            AwsOpenSearchClientFactory.Create(sp, sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenSearchOptions>>()));

        options.Services.AddTransient<OpenSearchSearchService>();
        options.Services.AddTransient<OpenSearchSearchIndexer>();

        options.Services.AddProvider<ISearchService>((provider, config) =>
        {
            if (!config.HasSearchType(SearchTypeName)) return null;
            return provider.GetRequiredService<OpenSearchSearchService>();
        });

        options.Services.AddProvider<ISearchIndexer>((provider, config) =>
        {
            if (!config.HasSearchType(SearchTypeName)) return null;
            return provider.GetRequiredService<OpenSearchSearchIndexer>();
        });

        return options;
    }

    public static NuGetApiOptions AutoDiscoverOpenSearch(this NuGetApiOptions options)
        => options.AddOpenSearch();
}

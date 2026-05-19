using AvantiPoint.Packages.Azure.Configuration;
using AvantiPoint.Packages.Azure.Search;
using AvantiPoint.Packages.Core;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages;

public static class AzureSearchApplicationExtensions
{
    public const string SearchTypeName = "AzureSearch";

    public static NuGetApiOptions AddAzureSearch(this NuGetApiOptions options)
    {
        options.Services.AddNuGetApiOptions<AzureSearchOptions>(nameof(PackageFeedOptions.Search));

        options.Services.AddSingleton(sp =>
        {
            var searchOptions = sp.GetRequiredService<IOptions<AzureSearchOptions>>().Value;
            var credential = new AzureKeyCredential(searchOptions.ApiKey);
            return new SearchIndexClient(new Uri(searchOptions.Endpoint), credential);
        });

        options.Services.AddSingleton(sp =>
        {
            var searchOptions = sp.GetRequiredService<IOptions<AzureSearchOptions>>().Value;
            var credential = new AzureKeyCredential(searchOptions.ApiKey);
            return new SearchClient(new Uri(searchOptions.Endpoint), searchOptions.IndexName, credential);
        });

        options.Services.AddTransient<AzureSearchService>();
        options.Services.AddTransient<AzureSearchIndexer>();

        options.Services.AddProvider<ISearchService>((provider, config) =>
        {
            if (!config.HasSearchType(SearchTypeName)) return null;
            return provider.GetRequiredService<AzureSearchService>();
        });

        options.Services.AddProvider<ISearchIndexer>((provider, config) =>
        {
            if (!config.HasSearchType(SearchTypeName)) return null;
            return provider.GetRequiredService<AzureSearchIndexer>();
        });

        return options;
    }

    public static NuGetApiOptions AutoDiscoverAzureSearch(this NuGetApiOptions options)
        => options.AddAzureSearch();
}

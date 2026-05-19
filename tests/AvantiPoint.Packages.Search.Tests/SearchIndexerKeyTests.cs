using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Search.Tests;

public class SearchIndexerKeyTests
{
    [Fact]
    public void NullSearchIndexer_UsesNullKey()
    {
        var indexer = new NullSearchIndexer();
        Assert.Equal(SearchIndexerKeys.Null, indexer.Key);
    }

    [Fact]
    public void SearchIndexerKeys_AreStable()
    {
        Assert.Equal("Null", SearchIndexerKeys.Null);
        Assert.Equal("AzureSearch", SearchIndexerKeys.AzureSearch);
        Assert.Equal("Elasticsearch", SearchIndexerKeys.Elasticsearch);
        Assert.Equal("OpenSearch", SearchIndexerKeys.OpenSearch);
        Assert.Equal(3, SearchIndexerKeys.CurrentSchemaVersion);
    }
}

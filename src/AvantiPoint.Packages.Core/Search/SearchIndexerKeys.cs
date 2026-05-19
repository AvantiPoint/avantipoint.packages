namespace AvantiPoint.Packages.Core;

/// <summary>
/// Stable keys for <see cref="ISearchIndexer"/> implementations.
/// One key per indexer class; <see cref="Null"/> is shared by Database and Null search types.
/// </summary>
public static class SearchIndexerKeys
{
    public const string Null = "Null";
    public const string AzureSearch = "AzureSearch";
    public const string Elasticsearch = "Elasticsearch";
    public const string OpenSearch = "OpenSearch";

    public const int CurrentSchemaVersion = 3;
}

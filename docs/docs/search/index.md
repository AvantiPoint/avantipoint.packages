# Search providers

AvantiPoint Packages supports several search backends. **`Search.Type`** selects how **queries** run (`ISearchService`). **`ISearchIndexer.Key`** tracks which external index last wrote each package version (`Package.IndexedWith`).

## Search types

| `Search.Type` | Query service | Indexer | Indexer key |
|---------------|---------------|---------|-------------|
| `Database` (default) | `DatabaseSearchService` | `NullSearchIndexer` (no external index) | `Null` |
| `Null` | `NullSearchService` | `NullSearchIndexer` | `Null` |
| `Elasticsearch` | `ElasticsearchSearchService` | `ElasticsearchSearchIndexer` | `Elasticsearch` |
| `OpenSearch` | `OpenSearchSearchService` (AWS) | `OpenSearchSearchIndexer` | `OpenSearch` |
| `AzureSearch` | `AzureSearchService` | `AzureSearchIndexer` | `AzureSearch` |

## When to use an external index

- **Database** — simplest deployment; search runs against SQL with no extra infrastructure.
- **Elasticsearch / OpenSearch / Azure AI Search** — better full-text relevance, lower database load on large feeds, and horizontal scaling of search workloads.

## Startup reconciliation

When an **external** indexer is active, a background service reindexes packages whose `IndexedWith` is null or does not match the current indexer key. The API starts immediately; search results may be incomplete until reconciliation finishes.

`NullSearchIndexer` (Database and Null search types) skips reconciliation entirely.

## Configuration

```json
{
  "Feed": {
    "Search": {
      "Type": "Elasticsearch",
      "ReconcileBatchSize": 100
    }
  }
}
```

See provider-specific guides:

- [Elasticsearch](elasticsearch.md)
- [OpenSearch (AWS)](opensearch.md)
- [Azure AI Search](azure-search.md)
- [Migrating from Database search](migration.md)

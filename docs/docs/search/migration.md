# Migrating search providers

## Database → external index

1. Deploy Elasticsearch, OpenSearch, or Azure AI Search.
2. Set `Feed:Search:Type` to `Elasticsearch`, `OpenSearch`, or `AzureSearch` and configure provider settings.
3. Restart the feed. Background reconciliation indexes every listed package whose `IndexedWith` is null or not equal to the new indexer key.
4. The API remains available during reconciliation; expect partial search results until reconciliation completes.

Existing `IndexedWith` values are typically `null` or `Null` from database search, so all packages are selected on the first run.

## Switching external providers

Changing `Search.Type` from e.g. `Elasticsearch` to `AzureSearch` changes the active indexer key. Packages with `IndexedWith = Elasticsearch` are reindexed because the key no longer matches.

## External index → Database

Set `Search.Type` to `Database`. Reconciliation stops (`NullSearchIndexer`). Search queries use SQL again. Old `IndexedWith` values pointing at external indexers are harmless.

## Schema changes

When the search document schema changes, `SearchIndexerKeys.CurrentSchemaVersion` is bumped. On startup, `IndexedWith` is cleared for all packages and a full reindex runs once.

## Database migration

Apply EF migrations so `Packages.IndexedWith` and `SearchIndexStates` exist:

```bash
dotnet ef database update --context SqliteContext --project src/AvantiPoint.Packages.Database.Sqlite
```

Use the context for your database provider (SqlServer, PostgreSql, MySql).

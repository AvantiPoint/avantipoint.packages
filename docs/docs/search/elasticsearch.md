# Elasticsearch search

Use self-hosted Elasticsearch or OpenSearch without AWS signing via `AvantiPoint.Packages.Elasticsearch`.

## Registration

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AutoDiscoverElasticsearchSearch();
});
```

## Configuration

```json
{
  "Feed": {
    "Search": {
      "Type": "Elasticsearch",
      "Endpoint": "https://localhost:9200",
      "IndexName": "packages",
      "Username": "elastic",
      "Password": "changeme",
      "DisableCertificateValidation": true,
      "ReconcileBatchSize": 100
    }
  }
}
```

The indexer creates the `packages` index on first use if it does not exist.

## Docker Compose (local)

```yaml
services:
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:8.15.0
    environment:
      - discovery.type=single-node
      - xpack.security.enabled=false
    ports:
      - "9200:9200"
```

Set `Search:Endpoint` to `http://localhost:9200`.

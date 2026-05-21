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

## Elasticsearch container

Run a single-node instance for development or a sidecar in your environment:

```bash
docker run -d -p 9200:9200 \
  -e discovery.type=single-node \
  -e xpack.security.enabled=false \
  docker.elastic.co/elasticsearch/elasticsearch:8.15.0
```

Set `Search:Endpoint` to your cluster URL (for example `http://localhost:9200` when port-mapping locally).

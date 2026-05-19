# OpenSearch (AWS)

AWS OpenSearch Service uses the same document model as Elasticsearch, with **SigV4** request signing via `AvantiPoint.Packages.Aws`.

## Registration

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AutoDiscoverOpenSearch();
});
```

## Configuration

```json
{
  "Feed": {
    "Search": {
      "Type": "OpenSearch",
      "Endpoint": "https://search-my-domain.us-east-1.es.amazonaws.com",
      "IndexName": "packages",
      "Region": "us-east-1",
      "UseIamAuth": true,
      "ReconcileBatchSize": 100
    }
  }
}
```

When `UseIamAuth` is `true`, the default AWS credential chain signs requests. Set `UseIamAuth` to `false` and provide `Username` / `Password` for basic authentication instead.

## IAM

Grant the task or instance role `es:ESHttp*` (or the equivalent data-plane permissions) on the domain resource.

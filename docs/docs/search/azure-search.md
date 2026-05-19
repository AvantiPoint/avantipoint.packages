# Azure AI Search

Azure AI Search is provided by `AvantiPoint.Packages.Azure` using `Azure.Search.Documents`.

## Registration

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AutoDiscoverAzureSearch();
});
```

## Configuration

```json
{
  "Feed": {
    "Search": {
      "Type": "AzureSearch",
      "Endpoint": "https://my-search.search.windows.net",
      "ApiKey": "<admin-key>",
      "IndexName": "packages",
      "ReconcileBatchSize": 100
    }
  }
}
```

Create a search service in Azure Portal, then create an index named `packages` (or rely on automatic index creation on first index operation).

## Index schema

The provider maps one document per package ID (all versions embedded), aligned with the NuGet search API shape. Fields include `Id`, `Versions`, `Tags`, `PackageTypes`, `Frameworks`, and `SearchFilters` for prerelease/SemVer2 filtering.

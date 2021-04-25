You can configure your package feed to consume one or more upstream sources. You may do this manually when registering your package api.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddNuGetPackagApi(app =>
    {
        app.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json")
           .AddUpstreamSource("Telerik", "https://nuget.telerik.com/nuget", "user@domain.com", "your-password");
    });
}
```

You may register your upstream sources automatically by adding them to your configuration as follows:

```json
{
  "mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    },
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Username": "user@domain.com",
      "ApiToken": "your-password"
    }
  }
}
```

!!! note
    If you manually register a source any sources configured as shown will not automatically be registered.
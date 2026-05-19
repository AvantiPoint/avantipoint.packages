# npm registry

The npm registry surface is registered in code and exposed at `/npm` by default.

## Registration

```csharp
var feed = builder.AddAvantiPointFeed(configuration.GetSection("Feed"));
feed.UseNuGet();
feed.UseNpmRegistry(routePrefix: "/npm", surfaceId: "npm");

var app = builder.Build();
app.MapNuGetApiRoutes();
app.MapNpmFeed(feed);
```

## Configuration

```json
{
  "Feed": {
    "Authentication": {
      "ApiKey": "your-token",
      "AllowAnonymousPull": false
    },
    "Npm": {
      "Mirror": {
        "RegistryUrl": "https://registry.npmjs.org"
      }
    }
  }
}
```

## Client usage

```bash
npm login --registry https://your-host/npm/
npm publish --registry https://your-host/npm/
npm install --registry https://your-host/npm/
```

Use the configured API key as a Bearer token (`NPM_TOKEN`) for CI scenarios.

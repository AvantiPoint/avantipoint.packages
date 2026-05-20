# npm registry

The npm registry surface is registered in code and exposed at `/npm` by default.

## Registration

```csharp
var feed = builder.AddAvantiPointFeed(configuration.GetSection("Feed"));
feed.UseNuGet();
feed.UseNpmRegistry(routePrefix: "/npm", surfaceId: "npm");

var app = builder.Build();
app.UseAvantiPointFeedPlatform();
app.UseRouting();
app.MapNuGetApiRoutes();
app.MapNpmFeed(feed);
```

Register `UseAvantiPointFeedPlatform()` **before** `UseRouting()` so OCI path rewriting and surface resolution run before endpoint matching.

## Configuration

```json
{
  "Feed": {
    "PublicBaseUrl": "https://packages.example.com/myfeed",
    "Authentication": {
      "ApiKey": "your-token",
      "AllowAnonymousPull": false
    },
    "Npm": {
      "MaxPublishBodyBytes": 104857600,
      "MaxTarballBytes": 104857600,
      "Mirror": {
        "RegistryUrl": "https://registry.npmjs.org"
      }
    }
  }
}
```

`PublicBaseUrl` overrides the origin used for tarball and packument URLs (use behind a reverse proxy). When unset, `X-Forwarded-Proto`, `X-Forwarded-Host`, and `X-Forwarded-Prefix` are honored, then `PathBase` / `PackageFeed:PathBase`.

`MaxPublishBodyBytes` and `MaxTarballBytes` default to 100 MB and reject oversized publishes with HTTP 413.

## Client usage

```bash
npm login --registry https://your-host/npm/
npm publish --registry https://your-host/npm/
npm install --registry https://your-host/npm/
```

Use the configured API key as a Bearer token (`NPM_TOKEN`) for CI scenarios.

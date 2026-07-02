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

## Upstream registries (authenticated / multiple)

`Mirror:RegistryUrl` configures a single unauthenticated upstream. For **authenticated upstreams** (for example FontAwesome Pro or Telerik npm) or **multiple upstreams with fallback**, use `Mirror:Registries` instead — registries are tried in ascending `Priority` order and the first hit wins:

```json
{
  "Feed": {
    "Npm": {
      "Mirror": {
        "Registries": [
          {
            "Url": "https://npm.fontawesome.com",
            "Token": "your-fontawesome-pro-token",
            "Priority": 0
          },
          {
            "Url": "https://registry.npmjs.org",
            "Priority": 10
          }
        ]
      }
    }
  }
}
```

Per registry:

- `Token` — sent as `Authorization: Bearer <token>` (equivalent to npm's `//host/:_authToken`). Takes precedence over basic credentials.
- `Username` / `Password` — sent as basic authentication when no `Token` is set.
- `Priority` — lower values are tried first.

Tarball downloads automatically reuse the credentials of the registry whose host matches the tarball URL, so authenticated tarball CDNs on the same host work without extra configuration. When `Registries` is non-empty, `RegistryUrl` is ignored.

This lets teams consume commercial scoped packages through the internal feed without configuring per-project `.npmrc` credentials — only the feed needs the upstream token.

## Client usage

```bash
npm login --registry https://your-host/npm/
npm publish --registry https://your-host/npm/
npm install --registry https://your-host/npm/
```

Use the configured API key as a Bearer token (`NPM_TOKEN`) for CI scenarios.

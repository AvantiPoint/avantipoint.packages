---
id: deployment-scenarios
title: Deployment Scenarios
sidebar_label: Deployment Scenarios
sidebar_position: 5
---

This guide describes common feed deployment patterns. Each scenario combines `PackageSources` caching strategies with `Search.IncludeMirroredPackages` to control what appears in browse/search versus what is available for restore.

For strategy details and the full behavior matrix, see [Upstream Mirrors](mirrors.md#caching-strategies-packagesourcecachingstrategy).

## How search and mirrors work together

Two settings control discovery versus restore:

| Setting | Location | Default | Effect |
|---------|----------|---------|--------|
| `CachingStrategy` | Per `PackageSource` | `IndexAndCache` | Controls disk usage, database metadata, and package origin |
| `IncludeMirroredPackages` | `Search` section | `true` | Controls whether `Mirrored` packages appear in search and registration discovery |

**Package origins:**

- `Published` — pushed directly to this feed; always eligible for search (when listed)
- `Mirrored` — downloaded and indexed from upstream (`IndexAndCache`); included in search when `IncludeMirroredPackages` is `true`
- `Cached` — stored for restore only (`CacheOnly`); never included in search

Packages from `ProxyOnly` sources are not stored locally and never appear in search.

## Strategy × search × disk matrix

| Caching strategy | `IncludeMirroredPackages: true` | `IncludeMirroredPackages: false` | Disk usage |
|------------------|--------------------------------|----------------------------------|------------|
| `IndexAndCache` | Appears in search | Hidden from search; restore works | Full (binary + metadata) |
| `CacheOnly` | Never in search | Never in search | Binary only (no DB/search) |
| `ProxyOnly` | Never in search | Never in search | None |
| Published (direct push) | Appears in search | Appears in search | Full |

Configure `PackageSource` rows via the Host UI (`/Account/PackageSources`), database seeding, or `Mirror:NuGetConfigPath`. The examples below show the equivalent `appsettings.json` values for each scenario.

---

## Commercial / private feed

**Goal:** Show only packages published to your feed in search, while still allowing clients to restore upstream dependencies (NuGet.org, commercial feeds, etc.) with minimal disk usage.

**Pattern:** `Search.IncludeMirroredPackages: false` + `ProxyOnly` (or `CacheOnly` if you want faster repeat restores without search visibility).

```json
{
  "Database": {
    "Type": "SqlServer"
  },
  "Storage": {
    "Type": "FileStorage",
    "Path": "App_Data"
  },
  "ConnectionStrings": {
    "SqlServer": "Server=...;Database=packages;..."
  },
  "Search": {
    "Type": "Database",
    "IncludeMirroredPackages": false
  },
  "PackageSources": [
    {
      "Name": "NuGet.org",
      "FeedUrl": "https://api.nuget.org/v3/index.json",
      "Type": "Upstream",
      "CachingStrategy": "ProxyOnly",
      "MirrorSignaturePolicy": "Merge",
      "IsEnabled": true
    },
    {
      "Name": "Telerik",
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Type": "Upstream",
      "CachingStrategy": "ProxyOnly",
      "MirrorSignaturePolicy": "Resign",
      "Username": "user@example.com",
      "Password": "your-api-key",
      "IsEnabled": true
    }
  ]
}
```

**Alternative with local cache:** Replace `ProxyOnly` with `CacheOnly` on upstream sources if you want faster repeat restores without adding packages to search. Disk usage grows with unique restored packages but stays lower than full indexing (no database metadata or search index entries).

---

## Enterprise mirror

**Goal:** Provide a single feed URL that includes both internally published packages and mirrored upstream packages in search—typical for corporate artifact hubs and offline-capable mirrors.

**Pattern:** `Search.IncludeMirroredPackages: true` (default) + `IndexAndCache` on upstream sources.

```json
{
  "Database": {
    "Type": "SqlServer"
  },
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;"
  },
  "ConnectionStrings": {
    "SqlServer": "Server=...;Database=packages;..."
  },
  "Search": {
    "Type": "Database",
    "IncludeMirroredPackages": true
  },
  "PackageSources": [
    {
      "Name": "NuGet.org",
      "FeedUrl": "https://api.nuget.org/v3/index.json",
      "Type": "Both",
      "CachingStrategy": "IndexAndCache",
      "MirrorSignaturePolicy": "Merge",
      "IsEnabled": true
    },
    {
      "Name": "InternalUpstream",
      "FeedUrl": "https://nuget.internal.example.com/v3/index.json",
      "Type": "Upstream",
      "CachingStrategy": "IndexAndCache",
      "MirrorSignaturePolicy": "TrustedCerts",
      "IsEnabled": true
    }
  ],
  "Shields": {
    "ServerName": "Contoso Packages"
  }
}
```

This is the **default behavior** for existing deployments: upstream packages are indexed and appear in search alongside published packages.

---

## Lightweight dev / Docker

**Goal:** Run a small feed for local development or containerized CI with published packages visible in search, upstream restore available, and minimal disk footprint.

**Pattern:** Mount the developer's NuGet global packages folder read-only, enable `LocalCache`, hide
mirrored packages from search, and use `ProxyOnly` for cache misses.

```json
{
  "Database": {
    "Type": "Sqlite"
  },
  "Storage": {
    "Type": "FileStorage",
    "Path": "/data/packages"
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=/data/packages.db"
  },
  "Search": {
    "Type": "Database",
    "IncludeMirroredPackages": false
  },
  "LocalCache": {
    "Enabled": true,
    "Path": "/nuget-cache",
    "CopyToFeedStorage": false
  },
  "Mirror": {
    "NuGetConfigPath": "/config/NuGet.config",
    "DefaultCachingStrategy": "ProxyOnly"
  }
}
```

Packages found in `/nuget-cache` are streamed directly without being copied into feed storage.
Packages not found locally are proxied from the sources in `NuGet.config`. Set
`LocalCache.CopyToFeedStorage` to `true` when cache hits should remain available after the cache
mount is removed.

The repository includes a complete [LightweightFeed sample](https://github.com/AvantiPoint/avantipoint.packages/tree/master/samples/LightweightFeed)
with SQLite, a minimal Docker image, a read-only global packages mount, a persistent `/data` volume,
and a health endpoint.

---

## Migration guide for existing deployments

If you are upgrading to v4 with package origin and caching strategy support, **no configuration changes are required** to preserve current behavior:

| Setting | Default | Existing behavior preserved |
|---------|---------|----------------------------|
| `Search.IncludeMirroredPackages` | `true` | Mirrored packages remain visible in search |
| `PackageSource.CachingStrategy` | `IndexAndCache` | Upstream packages are cached and indexed as before |

### Optional changes by scenario

| If you want… | Change |
|--------------|--------|
| Hide upstream packages from search (commercial/private feed) | Set `Search.IncludeMirroredPackages` to `false` |
| Restore upstream deps without search visibility or DB metadata | Set upstream `CachingStrategy` to `CacheOnly` |
| Restore upstream deps with no local disk usage | Set upstream `CachingStrategy` to `ProxyOnly` |
| Reuse the developer's existing global packages folder | Enable `LocalCache` and mount the folder read-only |
| Full enterprise mirror (current default) | Keep defaults (`IncludeMirroredPackages: true`, `IndexAndCache`) |

### Migrating from legacy `Mirror` configuration

The legacy `Mirror` dictionary and `AddUpstreamSource` helpers are deprecated in v4.0:

1. Create equivalent `PackageSource` rows (via Host UI or database) with `Name`, `FeedUrl`, `CachingStrategy`, and credentials
2. Or use `Mirror:NuGetConfigPath` to bootstrap sources from an existing `NuGet.config` on startup
3. Remove the old `Mirror` section from `appsettings.json` once sources are configured

Existing mirrored packages in the database retain their stored origin. New restores follow the `CachingStrategy` on each source.

---

## Unified multi-protocol host

Run NuGet, npm, and OCI (default + named segments) on one hostname with isolated storage prefixes:

```json
{
  "Feed": {
    "Name": "production",
    "Storage": {
      "Prefix": "feeds/production/"
    },
    "NuGet": { "Enabled": true },
    "Npm": {
      "Enabled": true,
      "IncludeMirroredPackages": false,
      "Mirror": { "RegistryUrl": "https://registry.npmjs.org" }
    },
    "Oci": {
      "Default": {
        "Enabled": true,
        "IncludeMirroredInCatalog": false
      },
      "Docker": { "Enabled": true },
      "Helm": { "Enabled": true }
    }
  },
  "Storage": {
    "Type": "FileSystem",
    "Path": "App_Data"
  }
}
```

Blob layout uses `feeds/{Feed.Name}/` for NuGet and npm (`npm/`), plus `oci/` and `oci/{segment}/` per OCI registration. Database rows are scoped by `FeedId` matching `Feed.Name`.

> **Future:** A `feeds[]` array for unrelated logical tenants on one process is planned; NuGet v3 must remain at `/` on each tenant hostname.

---

## Dedicated hostnames (Kubernetes Ingress)

Use separate Ingress rules when clients expect different URL shapes on the same backend deployment:

| Hostname | Routes | Typical clients |
|----------|--------|-----------------|
| `nuget.company.com` | `/v3/`, `/api/` only | `dotnet restore`, Visual Studio |
| `registry.company.com` | `/npm/`, `/v2/`, `/docker/v2/`, `/helm/v2/` | Docker, Helm, npm |

Example Ingress snippet (same Service, two hosts):

```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: openfeed
spec:
  rules:
    - host: nuget.company.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: openfeed
                port:
                  number: 8080
    - host: registry.company.com
      http:
        paths:
          - path: /
            pathType: Prefix
            backend:
              service:
                name: openfeed
                port:
                  number: 8080
```

Set `Feed:PublicBaseUrl` (or reverse-proxy forwarded headers) so generated npm tarball URLs and OCI token endpoints use the hostname clients actually call. The NuGet-only host can omit npm/OCI UI links; the full registry host exposes all protocol surfaces.

## See Also

- [Upstream Mirrors](mirrors.md) - Caching strategies, authentication, and troubleshooting
- [Configuration](configuration.md) - Full configuration reference
- [Hosting](hosting.md) - Docker and production deployment

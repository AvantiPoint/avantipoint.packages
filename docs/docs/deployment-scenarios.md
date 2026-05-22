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

**Pattern:** `Search.IncludeMirroredPackages: false` + `CacheOnly` or `ProxyOnly` on upstream sources. Point clients at the feed URL; upstream dependencies resolve transparently on restore.

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
  "Mirror": {
    "NuGetConfigPath": "/config/NuGet.config",
    "DefaultCachingStrategy": "CacheOnly"
  },
  "PackageSources": [
    {
      "Name": "NuGet.org",
      "FeedUrl": "https://api.nuget.org/v3/index.json",
      "Type": "Upstream",
      "CachingStrategy": "CacheOnly",
      "IsEnabled": true
    }
  ]
}
```

Use `ProxyOnly` instead of `CacheOnly` when you want zero disk growth from upstream restores (every restore streams from upstream).

> **Note:** Run the standard Host image with a mounted `/data` volume for local development or CI. Point `Storage.Path` and the Sqlite connection string at paths under `/data`, keep `Search.IncludeMirroredPackages` at `false`, and set upstream sources to `CacheOnly` (or `ProxyOnly` when you want zero disk growth from upstream restores). See [Hosting](hosting.md) for Docker run examples.

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
| Full enterprise mirror (current default) | Keep defaults (`IncludeMirroredPackages: true`, `IndexAndCache`) |

### Migrating from legacy `Mirror` configuration

The legacy `Mirror` dictionary and `AddUpstreamSource` helpers are deprecated in v4.0:

1. Create equivalent `PackageSource` rows (via Host UI or database) with `Name`, `FeedUrl`, `CachingStrategy`, and credentials
2. Or use `Mirror:NuGetConfigPath` to bootstrap sources from an existing `NuGet.config` on startup
3. Remove the old `Mirror` section from `appsettings.json` once sources are configured

Existing mirrored packages in the database retain their stored origin. New restores follow the `CachingStrategy` on each source.

## See Also

- [Upstream Mirrors](mirrors.md) - Caching strategies, authentication, and troubleshooting
- [Configuration](configuration.md) - Full configuration reference
- [Hosting](hosting.md) - Docker and production deployment

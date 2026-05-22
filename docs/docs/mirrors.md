---
id: mirrors
title: Upstream Mirrors
sidebar_label: Upstream Mirrors
sidebar_position: 7
---

AvantiPoint Packages can proxy or mirror packages from one or more upstream NuGet feeds. This is useful for:

- **Caching NuGet.org** - Improve build times and reduce external dependencies
- **Consolidating feeds** - Provide a single feed URL that includes packages from multiple sources
- **Commercial feeds** - Include authenticated feeds (Telerik, Infragistics, Syncfusion, etc.) in your private feed
- **Offline scenarios** - Cache packages for environments with limited internet access

## How It Works

When a client requests a package that doesn't exist in your local feed, AvantiPoint Packages will:

1. Check each configured upstream source in order
2. Download the package from the first source that has it
3. Cache it locally for future requests
4. Serve it to the client

This is transparent to the client - they just see a single feed with all packages.

## Configuration (v4 PackageSources model)

> Previous `Mirror` configuration and `AddUpstreamSource` helpers are **deprecated** in v4.0 in favor of the `PackageSources` model.

Configure upstream sources using the `PackageSources` section in `appsettings.json`:

```json
{
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
      "Name": "Telerik",
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Type": "Upstream",
      "CachingStrategy": "IndexAndCache",
      "MirrorSignaturePolicy": "Resign",
      "Username": "user@example.com",
      "Password": "your-password-or-api-key",
      "IsEnabled": true
    }
  ]
}
```

**Key fields:**
- `Name` – Unique name for the source.
- `FeedUrl` – The upstream service index URL (`/v3/index.json`).
- `Type` – `Upstream`, `Downstream`, or `Both`.
- `CachingStrategy` – `IndexAndCache`, `CacheOnly`, or `ProxyOnly`.
- `MirrorSignaturePolicy` – `Resign`, `Merge`, or `TrustedCerts`.
- `Username` / `Password` / `ApiKey` – Optional authentication.
- `IsEnabled` – Controls whether the source participates in mirroring.

For advanced scenarios, you can manage `PackageSource` rows directly via the database or the Host UI at `/Account/PackageSources`.

## Caching strategies (`PackageSourceCachingStrategy`)

Each upstream source has a `CachingStrategy` that controls how packages from that source are stored, indexed, and exposed in search. This works together with [`Search.IncludeMirroredPackages`](configuration.md#search-and-discovery) to determine what clients see when browsing the feed.

| Strategy | Package binary on disk | Database metadata | Search index | Package origin |
|----------|------------------------|-------------------|--------------|----------------|
| `IndexAndCache` (default) | Yes | Yes | Yes | `Mirrored` |
| `CacheOnly` | Yes | No | No | `Cached` (storage-only) |
| `ProxyOnly` | No | No | No | *(not stored locally)* |

### `IndexAndCache`

The default behavior. When a client restores a package that is not already local:

1. Download the package from the upstream source
2. Store the `.nupkg` in configured storage (file system, Azure Blob, S3, etc.)
3. Persist package metadata in the database with origin `Mirrored`
4. Index the package for search and registration discovery (subject to `Search.IncludeMirroredPackages`)

Subsequent restores are served from local storage. Packages remain available even if the upstream source is temporarily unavailable.

### `CacheOnly`

Use when you want faster repeat restores without growing search results or database metadata:

1. Download and store the package binary in storage
2. **Skip** database persistence and search indexing

Packages cached with this strategy are never returned by search, autocomplete, or registration discovery—regardless of `Search.IncludeMirroredPackages`. Clients can still restore packages they request by id and version.

### `ProxyOnly`

Use when you want upstream packages available for restore with **minimal disk usage**:

1. Stream the package from upstream on each restore request
2. Do **not** write to local storage, the database, or the search index

`ProxyOnly` is the built-in alternative to custom proxy implementations. There is no local cache; upstream availability and latency apply on every restore.

### Strategy × search × disk usage

How each strategy interacts with [`Search.IncludeMirroredPackages`](configuration.md#search-and-discovery):

| Caching strategy | `IncludeMirroredPackages: true` | `IncludeMirroredPackages: false` | Disk usage |
|------------------|--------------------------------|----------------------------------|------------|
| `IndexAndCache` | Appears in search (`Mirrored`) | Hidden from search; restore works | Grows with every unique restore |
| `CacheOnly` | Never appears in search | Never appears in search | Grows with every unique restore |
| `ProxyOnly` | Never appears in search | Never appears in search | None (streamed from upstream) |
| Published (direct push) | Appears in search | Appears in search | Grows with each push |

For deployment patterns that combine these settings, see [Deployment Scenarios](deployment-scenarios.md).

### NuGet.config (runtime-only sources)

If you set `Mirror:NuGetConfigPath`, the server will append enabled sources from that `NuGet.config` **at runtime only**:

- Entries are not written to the database; they are merged in-memory with configured `PackageSources`.
- Duplicate names are skipped (case-insensitive).
- `MirrorOptions.DefaultCachingStrategy` and `DefaultSignaturePolicy` apply. By default, that means strip-and-re-sign when a repository signing certificate is configured; when repository signing is disabled, upstream repository signatures are left intact.

## Authenticated Feeds

Many commercial package feeds require authentication. You can provide credentials in two ways:

### Basic Authentication

Provide username and password/API token:

**In configuration**:
```json
{
  "Mirror": {
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Username": "user@example.com",
      "ApiToken": "your-password"
    }
  }
}
```

**In code**:
```csharp
options.AddUpstreamSource("Telerik", "https://nuget.telerik.com/nuget", "user@example.com", "password");
```

### API Key Authentication

Some feeds use API keys instead of username/password. Provide the key in the `ApiToken` field:

```json
{
  "Mirror": {
    "MyFeed": {
      "FeedUrl": "https://my-feed.com/v3/index.json",
      "ApiToken": "my-api-key-here"
    }
  }
}
```

Or in code:
```csharp
options.AddUpstreamSource("MyFeed", "https://my-feed.com/v3/index.json", apiToken: "my-api-key");
```

## Common Upstream Sources

### NuGet.org

The official public NuGet feed (no authentication required):

```json
{
  "Mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    }
  }
}
```

### Telerik

Commercial UI components:

```json
{
  "Mirror": {
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Username": "your-telerik-email@example.com",
      "ApiToken": "your-telerik-api-key"
    }
  }
}
```

Get your credentials from the Telerik account portal.

### Infragistics

Commercial UI components:

```json
{
  "Mirror": {
    "Infragistics": {
      "FeedUrl": "https://packages.infragistics.com/nuget/licensed",
      "Username": "your-email@example.com",
      "ApiToken": "your-api-key"
    }
  }
}
```

### Syncfusion

Commercial UI components:

```json
{
  "Mirror": {
    "Syncfusion": {
      "FeedUrl": "https://nuget.syncfusion.com/nuget_xxxx/nuget/getsyncfusionpackages/syncfusion",
      "Username": "your-email@example.com",
      "ApiToken": "your-api-key"
    }
  }
}
```

Replace `xxxx` with your specific feed ID from Syncfusion.

### DevExpress

Commercial UI components:

```json
{
  "Mirror": {
    "DevExpress": {
      "FeedUrl": "https://nuget.devexpress.com/api",
      "Username": "DevExpress",
      "ApiToken": "your-feed-token"
    }
  }
}
```

## Multiple Sources

You can configure as many upstream sources as you need:

```json
{
  "Mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    },
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Username": "user@example.com",
      "ApiToken": "key"
    },
    "Infragistics": {
      "FeedUrl": "https://packages.infragistics.com/nuget/licensed",
      "Username": "user@example.com",
      "ApiToken": "key"
    },
    "MyCompanyFeed": {
      "FeedUrl": "https://nuget.mycompany.com/v3/index.json"
    }
  }
}
```

When a package is requested, AvantiPoint Packages will search sources in the order they are defined (though this order is not guaranteed, so don't rely on it for priority).

## Caching behavior by strategy

What happens after a client restores a package from upstream depends on the source's `CachingStrategy`:

**`IndexAndCache`** (default):

1. Package binary is stored in local storage
2. Metadata is persisted in the database with origin `Mirrored`
3. The package is indexed for search (unless filtered by `Search.IncludeMirroredPackages`)
4. Future restores are served from local storage

**`CacheOnly`**:

1. Package binary is stored in local storage
2. Database metadata and search indexing are skipped
3. Future restores are served from local storage
4. The package never appears in browse/search

**`ProxyOnly`**:

1. Package is streamed from upstream for that request only
2. Nothing is written to storage, the database, or the search index
3. Each restore may contact the upstream source again

For `IndexAndCache` and `CacheOnly`, locally stored packages remain available even if the upstream source is temporarily unavailable.

## Proxy-only upstream sources

To always proxy without caching, set `CachingStrategy` to `ProxyOnly` on the upstream source. No custom implementation is required.

```json
{
  "PackageSources": [
    {
      "Name": "NuGet.org",
      "FeedUrl": "https://api.nuget.org/v3/index.json",
      "Type": "Upstream",
      "CachingStrategy": "ProxyOnly",
      "IsEnabled": true
    }
  ]
}
```

You can also apply `ProxyOnly` to all sources loaded from `NuGet.config` by setting `Mirror:DefaultCachingStrategy`:

```json
{
  "Mirror": {
    "NuGetConfigPath": "/config/NuGet.config",
    "DefaultCachingStrategy": "ProxyOnly"
  }
}
```

## Security Considerations

### Credential Storage

Store credentials securely:

**Don't do this** (credentials in source control):
```json
{
  "Mirror": {
    "Telerik": {
      "Username": "myemail@example.com",
      "ApiToken": "my-secret-password"
    }
  }
}
```

**Do this** (use environment variables or Azure Key Vault):

Set environment variables:
```bash
export Mirror__Telerik__Username="myemail@example.com"
export Mirror__Telerik__ApiToken="my-secret-password"
```

Or in Azure App Service Application Settings:
```
Mirror__Telerik__Username = myemail@example.com
Mirror__Telerik__ApiToken = my-secret-password
```

Then in `appsettings.json`:
```json
{
  "Mirror": {
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget"
    }
  }
}
```

### License Compliance

When mirroring commercial feeds:
- Ensure you have proper licenses for the packages
- Don't redistribute commercial packages beyond your licensed users
- Check the license agreement of each commercial vendor

### NuGet.config Files

When using `NuGetConfigPath` to load sources:

**Security:**
- Only use `ClearTextPassword` in NuGet.config files stored securely (not in source control)
- Consider using environment variables for sensitive config file paths
- Store NuGet.config files in protected directories with appropriate file permissions

**Example with environment variable:**
```json
{
  "Mirror": {
    "TeamSources": {
      "NuGetConfigPath": "%TEAM_NUGET_CONFIG%"
    }
  }
}
```

**Encrypted passwords:**
- NuGet CLI can encrypt passwords, but AvantiPoint Packages **cannot decrypt them**
- Sources with encrypted passwords will be automatically skipped with a warning log
- If you see "Skipping source... password is encrypted" warnings, use `ClearTextPassword` instead
- Sources pulled from NuGet.config are runtime-only; they will not be persisted to the `PackageSources` table.

## Troubleshooting

### "Package not found" even though it exists on NuGet.org

1. Check that your upstream source is configured correctly
2. Verify you can access the upstream feed from your server
3. Check logs for any authentication errors
4. Try searching for the package directly in your feed

### Authentication failures

1. Verify your credentials are correct
2. Check if the feed requires a specific authentication method (basic auth vs API key)
3. Some feeds may have IP restrictions - check with the vendor

### Slow package installs

1. First install will be slow (downloading from upstream)
2. Subsequent installs should be fast (served from cache)
3. Consider pre-populating your feed with common packages

## Best Practices

1. **Always include NuGet.org** - Most .NET packages come from there
2. **Use environment variables** - Don't commit credentials
3. **Monitor storage usage** - Cached packages accumulate over time
4. **Document your sources** - Keep track of what feeds you're using and why
5. **Test authentication** - Verify credentials before deploying to production

## Pre-populating the Cache

You can pre-populate your feed by pushing packages manually:

```bash
# Download package from upstream
nuget install Newtonsoft.Json -Version 13.0.1

# Push to your feed
dotnet nuget push Newtonsoft.Json.13.0.1.nupkg --source http://localhost:5000/v3/index.json
```

Or use a script to bulk-import common packages.

## npm upstream mirror

Configure pull-through for the npm registry surface:

```json
{
  "Feed": {
    "Npm": {
      "Enabled": true,
      "IncludeMirroredPackages": false,
      "Mirror": {
        "RegistryUrl": "https://registry.npmjs.org"
      }
    }
  }
}
```

On packument or tarball cache miss, OpenFeed fetches from `RegistryUrl`, stores content per the active mirror policy, and sets package `Origin` to `Mirrored` or `Cached`. When `IncludeMirroredPackages` is `false`, `/-/v1/search` and the npm browse UI list published packages only; clients can still install mirrored packages.

## OCI upstream mirror

Configure pull-through per OCI registration (default or named segment):

```json
{
  "Feed": {
    "Oci": {
      "Default": {
        "Enabled": true,
        "IncludeMirroredInCatalog": false,
        "Mirror": {
          "Registries": [
            {
              "Url": "https://registry-1.docker.io",
              "Priority": 0
            }
          ]
        }
      },
      "Docker": {
        "Enabled": true,
        "Mirror": {
          "Registries": [
            { "Url": "https://my-company.azurecr.io", "Username": "user", "Password": "token", "Priority": 0 }
          ]
        }
      }
    }
  }
}
```

On manifest or blob cache miss, the registry fetches from the lowest `Priority` upstream, streams to the client, and optionally persists blobs and tags. Mirrored tags respect `IncludeMirroredInCatalog` in `_catalog` and tag list APIs.

## See Also

- [Deployment Scenarios](deployment-scenarios.md) - Commercial, enterprise mirror, and lightweight dev/Docker patterns
- [Configuration](configuration.md) - Overall configuration guide
- [Storage](storage/index.md) - Configure package storage
- [Getting Started](getting-started.md) - Quick start guide

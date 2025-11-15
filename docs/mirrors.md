# Upstream Mirrors

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

## Configuration

### Via appsettings.json

Configure upstream sources in your `appsettings.json`:

```json
{
  "Mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    },
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Username": "user@example.com",
      "ApiToken": "your-password-or-api-key"
    },
    "MyOtherFeed": {
      "FeedUrl": "https://my-other-feed.com/v3/index.json",
      "Username": "user",
      "ApiToken": "token"
    }
  }
}
```

No code changes needed - sources are automatically registered from configuration.

### Via Code

Register sources programmatically in `Program.cs`:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
    
    // Add upstream sources
    options.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json");
    options.AddUpstreamSource("Telerik", "https://nuget.telerik.com/nuget", "user@example.com", "password");
});
```

**Important**: If you manually register sources via code, the configuration-based sources will be ignored.

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

## Caching Behavior

Once a package is downloaded from an upstream source:

1. It's stored in your local storage (file system, Azure Blob, or S3)
2. It's indexed in your local database
3. Future requests are served from local storage (much faster)
4. The package is retained even if removed from the upstream source

This provides:
- **Performance** - No need to contact upstream sources for cached packages
- **Reliability** - Your feed continues working even if upstream sources are unavailable
- **Offline capability** - Cached packages are available without internet access

## Disabling Caching

If you want to always proxy without caching (not recommended), you would need to customize the implementation. The default behavior is to cache all packages.

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

## See Also

- [Configuration](configuration.md) - Overall configuration guide
- [Storage](storage.md) - Configure package storage
- [Getting Started](getting-started.md) - Quick start guide
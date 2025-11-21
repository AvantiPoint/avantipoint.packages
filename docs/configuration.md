---
id: configuration
title: Configuration
sidebar_label: Full Configuration
sidebar_position: 4
---

AvantiPoint Packages is configured through a combination of `appsettings.json` and the fluent API in your `Program.cs` file.

## Basic Configuration Structure

Your `appsettings.json` should include these sections:

```json
{
  "Database": {
    "Type": "Sqlite"
  },
  "Storage": {
    "Type": "FileStorage",
    "Path": "App_Data"
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=packages.db",
    "SqlServer": "Server=(localdb)\\mssqllocaldb;Database=AvantiPointPackages;Trusted_Connection=True;",
    "MySql": "Server=localhost;Database=packages;User=root;Password=password;"
  },
  "Mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    }
  },
  "Shields": {
    "ServerName": "My Package Feed"
  }
}
```

## Database Configuration

AvantiPoint Packages supports three database providers:

### SQLite

Perfect for development and small deployments:

```json
{
  "Database": {
    "Type": "Sqlite"
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=packages.db"
  }
}
```

In `Program.cs`:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddSqliteDatabase("Sqlite");
});
```

### SQL Server

Recommended for production Windows deployments:

```json
{
  "Database": {
    "Type": "SqlServer"
  },
  "ConnectionStrings": {
    "SqlServer": "Server=myserver;Database=AvantiPointPackages;User Id=sa;Password=yourpassword;"
  }
}
```

In `Program.cs`:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddSqlServerDatabase("SqlServer");
});
```

### MySQL / MariaDB

Great for cross-platform production deployments:

```json
{
  "Database": {
    "Type": "MySql"
  },
  "ConnectionStrings": {
    "MySql": "Server=localhost;Database=packages;User=root;Password=password;"
  }
}
```

In `Program.cs`:

```csharp
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

builder.Services.AddNuGetPackageApi(options =>
{
    // For MySQL
    options.AddMySqlDatabase("MySql", ServerVersion.AutoDetect(connectionString));
    
    // Or for MariaDB
    options.AddMariaDbDatabase("MySql", ServerVersion.AutoDetect(connectionString));
});
```

### Connection String by Name

You can reference connection strings by name from the `ConnectionStrings` section:

```csharp
options.AddSqlServerDatabase("SqlServer"); // Uses ConnectionStrings:SqlServer
```

### Environment-Specific Configuration

Use different databases for different environments:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    if (options.Environment.IsDevelopment())
    {
        options.AddSqliteDatabase("Sqlite");
    }
    else
    {
        options.AddSqlServerDatabase("SqlServer");
    }
});
```

Or use `appsettings.{Environment}.json`:

**appsettings.Development.json**:
```json
{
  "Database": {
    "Type": "Sqlite"
  }
}
```

**appsettings.Production.json**:
```json
{
  "Database": {
    "Type": "SqlServer"
  }
}
```

## Storage Configuration

### File Storage

Store packages on the local file system:

```json
{
  "Storage": {
    "Type": "FileStorage",
    "Path": "App_Data"
  }
}
```

In `Program.cs`:

```csharp
options.AddFileStorage();
```

The path can be absolute or relative to the application directory. You can also specify a network share:

```json
{
  "Storage": {
    "Type": "FileStorage",
    "Path": "\\\\server\\share\\packages"
  }
}
```

### Azure Blob Storage

Store packages in Azure:

First, add the package:

```bash
dotnet add package AvantiPoint.Packages.Azure
```

Configuration:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "my-nuget-feed",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

Or use account name and key separately:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "my-nuget-feed",
    "AccountName": "mystorageaccount",
    "AccessKey": "your-access-key"
  }
}
```

In `Program.cs`:

```csharp
using AvantiPoint.Packages.Azure;

options.AddAzureBlobStorage();
```

### AWS S3 Storage

Store packages in Amazon S3:

First, add the package:

```bash
dotnet add package AvantiPoint.Packages.Aws
```

Configuration:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-nuget-feed",
    "Prefix": "packages",
    "UseInstanceProfile": true
  }
}
```

For explicit credentials:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-2",
    "Bucket": "my-nuget-feed",
    "AccessKey": "your-access-key",
    "SecretKey": "your-secret-key"
  }
}
```

In `Program.cs`:

```csharp
using AvantiPoint.Packages.Aws;

options.AddAwsS3Storage();
```

## Upstream Mirrors

Configure upstream package sources to mirror or proxy:

### Via Configuration

```json
{
  "Mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    },
    "Telerik": {
      "FeedUrl": "https://nuget.telerik.com/nuget",
      "Username": "user@example.com",
      "ApiToken": "your-password"
    }
  }
}
```

### Via Code

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

**Note**: Sources added via code will override configuration-based sources.

## Shields.io Badges

Enable package version badges:

```json
{
  "Shields": {
    "ServerName": "My Feed"
  }
}
```

With this enabled, you can use badges in your documentation:

```markdown
![Version](https://my-feed.example.com/api/shields/v/MyPackage.svg)
```

If `ServerName` is null or empty, the shields endpoint is disabled.

## Advanced Options

### Package Upload Size Limits

AvantiPoint Packages does not currently expose a separate `MaxPackageSize` setting. Package upload size is controlled by your hosting environment:

- **Kestrel / self-hosted**: Configure `MaxRequestBodySize` in `Program.cs`:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    // Example: allow up to 512 MB uploads
    options.Limits.MaxRequestBodySize = 512L * 1024L * 1024L;
});
```

- **IIS / Windows hosting**: Configure `maxAllowedContentLength` in `web.config` (in bytes). See the [Hosting](./hosting.md) guide for examples.

- **Reverse proxies (Nginx, etc.)**: Ensure the proxy is configured to allow the desired body size (for example, `client_max_body_size` in Nginx). See the [Hosting](./hosting.md) guide for details.

### Package Deletion

Allow or disallow package deletion (default: allow):

```json
{
  "PackageDeletion": {
    "Enabled": false
  }
}
```

### Search

Configure package search behavior:

```json
{
  "Search": {
    "Type": "Database"
  }
}
```

### Repository Package Signing

Enable repository-level package signing:

```json
{
  "Signing": {
    "Mode": "SelfSigned",
    "CertificatePasswordSecret": "Signing:CertificatePassword",
    "TimestampServerUrl": "http://timestamp.digicert.com",
    "UpstreamSignature": "ReSign",
    "SelfSigned": {
      "SubjectName": "CN=My Repository Signer, O=MyOrg, C=US",
      "KeySize": "KeySize4096",
      "ValidityInDays": 3650,
      "CertificatePath": "certs/repository-signing.pfx"
    }
  }
}
```

**Signing Modes:**
- `SelfSigned` - Automatically generate and manage certificates
- `StoredCertificate` - Use existing certificate from file or certificate store
- `AzureKeyVault` - Use certificate from Azure Key Vault (requires `AvantiPoint.Packages.Signing.Azure` package)
- `AwsKms` - Use AWS KMS with HSM-backed keys (requires `AvantiPoint.Packages.Signing.Aws` package)
- `AwsSigner` - Use AWS Signer managed code signing (requires `AvantiPoint.Packages.Signing.Aws` package)
- `GcpKms` - Use Google Cloud KMS with HSM protection (requires `AvantiPoint.Packages.Signing.Gcp` package)
- `GcpHsm` - Use Google Cloud HSM (requires `AvantiPoint.Packages.Signing.Gcp` package)

**Upstream Signature Behavior:**
- `ReSign` (default) - Strip existing repository signatures and replace with our own
- `Reject` - Reject packages that already have repository signatures

For detailed signing configuration, see [Package Signing](signing.md).

## Complete Example

Here's a complete production configuration:

**appsettings.Production.json**:

```json
{
  "Database": {
    "Type": "SqlServer"
  },
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
  },
  "ConnectionStrings": {
    "SqlServer": "Server=tcp:myserver.database.windows.net,1433;Database=packages;User ID=admin;Password=...;Encrypt=true;"
  },
  "Mirror": {
    "NuGet.org": {
      "FeedUrl": "https://api.nuget.org/v3/index.json"
    }
  },
  "Shields": {
    "ServerName": "Contoso Packages"
  }
}
```

**Program.cs**:

```csharp
using AvantiPoint.Packages;
using AvantiPoint.Packages.Azure;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPackageAuthenticationService, MyAuthService>();
builder.Services.AddScoped<INuGetFeedActionHandler, MyActionHandler>();

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddAzureBlobStorage();
    options.AddSqlServerDatabase("SqlServer");
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseRouting();
app.MapNuGetApiRoutes();

await app.RunAsync();
```

## See Also

- [Database](database.md) - Detailed database configuration
- [Storage](storage.md) - Detailed storage configuration
- [Upstream Mirrors](mirrors.md) - Detailed mirror configuration
- [Package Signing](signing.md) - Detailed signing configuration

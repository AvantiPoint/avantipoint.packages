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
app.UseOperationCancelledMiddleware();
app.MapNuGetApiRoutes();

await app.RunAsync();
```

## See Also

- [Database](database/index.md) - Detailed database configuration
- [Storage](storage/index.md) - Detailed storage configuration
- [Upstream Mirrors](mirrors.md) - Detailed mirror configuration

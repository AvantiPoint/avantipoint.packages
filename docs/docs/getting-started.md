---
id: getting-started
title: Getting Started
sidebar_label: Getting Started
sidebar_position: 2
---

This guide will walk you through setting up your first AvantiPoint Packages NuGet feed.

## Prerequisites

- .NET 10.0 SDK or later
- A database (SQLite for development, SQL Server or MySQL for production)
- (Optional) Cloud storage account (AWS S3 or Azure Blob Storage)

## Quick Start: Open Feed

The simplest way to get started is with an open, unauthenticated feed. This is perfect for development and testing.

### 1. Create a new ASP.NET Core application

```bash
dotnet new web -n MyNuGetFeed
cd MyNuGetFeed
```

### 2. Add required packages

```bash
dotnet add package AvantiPoint.Packages.Hosting
dotnet add package AvantiPoint.Packages.Database.Sqlite
```

### 3. Configure your feed

Update `Program.cs`:

```csharp
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
});

var app = builder.Build();

// In development, ensure database is created
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IContext>();
    db.Database.EnsureCreated();
}

app.UseRouting();
app.MapNuGetApiRoutes();

await app.RunAsync();
```

### 4. Configure database connection

Create or update `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Database": {
    "Type": "Sqlite"
  },
  "ConnectionStrings": {
    "Sqlite": "Data Source=packages.db"
  }
}
```

### 5. Run your feed

```bash
dotnet run
```

Your feed is now running! By default, it will be available at `https://localhost:5001/v3/index.json`.

## Testing Your Feed

### Add the feed as a source

```bash
dotnet nuget add source https://localhost:5001/v3/index.json --name MyLocalFeed
```

### Push a package

```bash
dotnet nuget push MyPackage.1.0.0.nupkg --source MyLocalFeed
```

### Search for packages

Navigate to `https://localhost:5001/v3/search?q=MyPackage` in your browser or use:

```bash
dotnet add package MyPackage --source MyLocalFeed
```

## Next Steps

- [Add Authentication](authentication.md) - Secure your feed
- [Configure Database](database/index.md) - Use SQL Server or MySQL in production
- [Configure Storage](storage/index.md) - Use cloud storage (S3 or Azure)
- [Add Callbacks](callbacks.md) - React to upload/download events
- [Configure Mirrors](mirrors.md) - Add upstream sources like NuGet.org

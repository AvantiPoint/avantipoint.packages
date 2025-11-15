# Service Registration

When setting up a package feed you must call the `AddNuGetPackageApi` extension method in your application startup. This registers all the core services needed to run a NuGet feed.

## Basic Registration

The simplest registration looks like this:

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

app.UseRouting();
app.UseOperationCancelledMiddleware();
app.MapNuGetApiRoutes();

await app.RunAsync();
```

## Configuration Callback

The `AddNuGetPackageApi` method accepts an optional configuration callback where you can:

- Configure the database provider
- Configure the storage provider
- Add upstream package sources
- Access the host environment and configuration

### Database Provider

You must configure one database provider. See [Database Configuration](database.md) for details.

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    // Choose one:
    options.AddSqliteDatabase("Sqlite");
    options.AddSqlServerDatabase("SqlServer");
    options.AddMySqlDatabase("MySql", serverVersion);
    options.AddMariaDbDatabase("MariaDb", serverVersion);
});
```

### Storage Provider

You must configure one storage provider. See [Storage Configuration](storage.md) for details.

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    // Choose one:
    options.AddFileStorage();           // Local file system
    options.AddAzureBlobStorage();      // Azure Blob Storage
    options.AddAwsS3Storage();          // AWS S3
});
```

### Upstream Sources

Optionally add one or more upstream package sources. See [Upstream Mirrors](mirrors.md) for details.

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
    
    // Add upstream sources
    options.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json");
    options.AddUpstreamSource("Telerik", "https://nuget.telerik.com/nuget", "user", "password");
});
```

### Environment-Specific Configuration

The configuration callback provides access to the host environment and configuration:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    
    // Use different databases for different environments
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

Or read from configuration:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    
    var dbType = options.Options.Database.Type;
    switch (dbType)
    {
        case "SqlServer":
            options.AddSqlServerDatabase("SqlServer");
            break;
        case "MySql":
            options.AddMySqlDatabase("MySql", ServerVersion.AutoDetect(connectionString));
            break;
        default:
            options.AddSqliteDatabase("Sqlite");
            break;
    }
});
```

## Additional Services

### Authentication

Register your authentication service before calling `AddNuGetPackageApi`:

```csharp
builder.Services.AddScoped<IPackageAuthenticationService, MyAuthService>();
builder.Services.AddNuGetPackageApi(options => { /* ... */ });
```

See [Authentication](authentication.md) for details.

### Callbacks

Register your action handler before calling `AddNuGetPackageApi`:

```csharp
builder.Services.AddScoped<INuGetFeedActionHandler, MyActionHandler>();
builder.Services.AddNuGetPackageApi(options => { /* ... */ });
```

See [Callbacks](callbacks.md) for details.

### HttpContextAccessor

If your authentication service or action handler needs access to the HTTP context:

```csharp
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPackageAuthenticationService, MyAuthService>();
```

## Middleware and Routing

After building your application, you must configure the middleware pipeline:

```csharp
var app = builder.Build();

// Recommended middleware order:
app.UseHttpsRedirection();          // Force HTTPS (production)
app.UseRouting();                    // Required

// Your custom middleware can go here

app.UseOperationCancelledMiddleware(); // Handle cancelled operations
app.MapNuGetApiRoutes();              // Map NuGet protocol routes

await app.RunAsync();
```

## Database Initialization

In development, you may want to automatically create the database:

```csharp
if (app.Environment.IsDevelopment())
{
#if DEBUG
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IContext>();
    db.Database.EnsureCreated();
#endif
    app.UseDeveloperExceptionPage();
}
```

For production, use EF Core migrations:

```bash
dotnet ef migrations add InitialCreate --context Context
dotnet ef database update --context Context
```

## Complete Example

Here's a complete example with all features:

```csharp
using AvantiPoint.Packages;
using AvantiPoint.Packages.Azure;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using MyApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPackageAuthenticationService, MyAuthService>();
builder.Services.AddScoped<INuGetFeedActionHandler, MyActionHandler>();

// Configure NuGet package API
builder.Services.AddNuGetPackageApi(options =>
{
    if (options.Environment.IsDevelopment())
    {
        options.AddFileStorage();
        options.AddSqliteDatabase("Sqlite");
        options.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json");
    }
    else
    {
        options.AddAzureBlobStorage();
        options.AddSqlServerDatabase("SqlServer");
    }
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    
    // Auto-create database in development
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IContext>();
    db.Database.EnsureCreated();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseOperationCancelledMiddleware();
app.MapNuGetApiRoutes();

await app.RunAsync();
```

## See Also

- [Getting Started](getting-started.md) - Quick start guide
- [Configuration](configuration.md) - Detailed configuration options
- [Authentication](authentication.md) - User authentication
- [Callbacks](callbacks.md) - Event handlers
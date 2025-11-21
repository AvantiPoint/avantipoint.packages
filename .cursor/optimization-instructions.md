# AvantiPoint.Packages - Cursor AI Optimization Instructions

This document provides context and guidelines for optimizing AI-assisted development sessions in this repository.

## Repository Overview

**AvantiPoint.Packages** is a modern .NET 10 NuGet feed solution (forked from BaGet) that provides:
- Self-hosted NuGet package feeds
- Custom authentication for package consumers and publishers
- Extensibility hooks via callbacks
- Multiple storage backends (Azure Blob, AWS S3, File System)
- Multiple database providers (SQL Server, SQLite, MySQL)
- Repository-level package signing

### Key Technologies
- **.NET 10.0** (see `global.json` for exact SDK version)
- **ASP.NET Core** (web API, middleware, hosting)
- **Entity Framework Core** (database operations)
- **NuGet.Protocol** (NuGet client operations)
- **NuGet.Packaging** (package signing, reading, writing)
- **Central Package Management** (all versions in `Directory.Packages.props`)

---

## Project Structure

### Source Projects (`src/`)

1. **AvantiPoint.Packages.Core** - Core abstractions and implementations
   - `Authentication/` - Package authentication services
   - `Configuration/` - Options classes for configuration
   - `Content/` - Package content service implementations
   - `Entities/` - EF Core entity models
   - `Extensions/` - Extension methods (DI, providers, etc.)
   - `Indexing/` - Package indexing and processing
   - `Metadata/` - Package metadata services
   - `Mirror/` - Upstream mirroring functionality
   - `Search/` - Search service implementations
   - `ServiceIndex/` - NuGet service index
   - `Signing/` - Repository signing implementation
   - `Storage/` - Storage service abstractions
   - `Validation/` - Validation services

2. **AvantiPoint.Packages.Hosting** - ASP.NET Core hosting integration
   - `Apis/` - API endpoint implementations (REST endpoints)
   - `Authentication/` - ASP.NET Core authentication middleware
   - `Extensions/` - Hosting extension methods
   - `Middleware/` - Custom middleware
   - API routes defined in `Routes.cs`
   - Main API mapping in `PackagesApi.cs`

3. **AvantiPoint.Packages.Protocol** - NuGet protocol implementation
   - `Models/` - Protocol model classes
   - `Catalog/`, `PackageContent/`, `PackageMetadata/`, `Publish/`, `Search/`, `ServiceIndex/`, `Vulnerability/` - Protocol-specific implementations
   - `NuGetClient.cs` - Main client implementation

4. **AvantiPoint.Packages.Database.*** - Database provider implementations
   - Each has: `*ApplicationExtensions.cs`, `*Context.cs`, `*ContextFactory.cs`
   - Migrations in `Migrations/` folder

5. **AvantiPoint.Packages.Azure** / **AvantiPoint.Packages.Aws** - Cloud storage providers
   - `*ApplicationExtensions.cs` - DI registration
   - `Configuration/` - Options classes
   - `Storage/` - Storage service implementations

6. **AvantiPoint.Packages.UI.Razor** - Blazor UI components

### Test Projects (`tests/`)
- `AvantiPoint.Packages.Tests` - Core tests
- `AvantiPoint.Packages.Protocol.Tests` - Protocol tests
- `AvantiPoint.Packages.UI.Tests` - UI tests
- Uses **xUnit**, **Moq**, **EF Core InMemory**

### Samples (`samples/`)
- `OpenFeed` - Simple open feed without authentication
- `AuthenticatedFeed` - Secured feed with authentication
- `SampleDataGenerator` - Tool for generating test data

---

## Code Style and Conventions

### File-Scoped Namespaces (NEW CODE ONLY)
- ✅ **All NEW code** must use file-scoped namespaces: `namespace AvantiPoint.Packages.Core.Signing;`
- ⚠️ **Existing code** may use traditional namespace blocks - do NOT refactor unless explicitly requested
- EditorConfig enforces this with `IDE0160` and `IDE0161` warnings

### Indentation and Formatting
- **Spaces, not tabs** - 4 spaces for C# code, 2 spaces for XML/JSON/YAML
- Use `.editorconfig` settings (already configured)
- Always insert final newline
- Trim trailing whitespace

### Single Type Per File
- One top-level public type per file (class, interface, struct, enum, record)
- Supporting types should be in separate files or nested if truly private
- Partial classes are acceptable for organization (e.g., `DependencyInjectionExtensions.cs` + `DependencyInjectionExtensions.Providers.cs`)

### Modern C# 14 Features (Required for New Code)

This codebase targets **.NET 10 with C# 14**, and all new code should leverage the latest language features:

#### Collection Expressions (C# 12/14)
- ✅ **Always use collection expressions**: `[]` instead of `new List<string>()`
- ✅ Use spread operator: `[..existingList, newItem]` instead of `existingList.Concat([newItem]).ToList()`
- ✅ Prefer `[]` for empty collections: `[]` instead of `new List<string>()` or `Array.Empty<string>()`
- ✅ Use for arrays: `string[] items = ["a", "b", "c"];` instead of `new[] { "a", "b", "c" }`

#### Primary Constructors (C# 12)
- ✅ **Use primary constructors** for DI-only classes (no initialization logic beyond field assignment):
  ```csharp
  public class MyService(ILogger<MyService> logger, IOptions<MyOptions> options)
  {
      private readonly ILogger<MyService> _logger = logger;
      private readonly MyOptions _options = options.Value;
  }
  ```
- ⚠️ Use traditional constructors when you need validation, derived state, or complex initialization

#### File-Scoped Namespaces (C# 10)
- ✅ **Always use file-scoped namespaces** in new code: `namespace AvantiPoint.Packages.Core.Signing;`
- ⚠️ Don't refactor existing traditional namespace blocks unless explicitly requested

#### Pattern Matching (C# 7+)
- ✅ Use `is not null` / `is null` instead of `!= null` / `== null`
- ✅ Use pattern matching: `if (x is string s)` instead of `if (x is string) { var s = (string)x; }`
- ✅ Use switch expressions: `var result = x switch { 1 => "one", 2 => "two", _ => "other" };`
- ✅ Use property patterns: `if (obj is { Property: "value" })`
- ✅ Use list patterns: `if (items is [var first, .., var last])`

#### Modern Language Features
- ✅ Use expression-bodied members: `public string Name => _name;` for simple properties
- ✅ Use null-conditional operators: `obj?.Property?.Method()`
- ✅ Use string interpolation: `$"Value: {value}"` (not `string.Format`)
- ✅ Use `var` when type is apparent: `var list = new List<string>();` → `var list = [];`
- ✅ Use target-typed `new`: `List<string> list = new();` (though collection expressions are preferred)
- ✅ Use `nameof()` for property names: `nameof(MyProperty)` instead of `"MyProperty"`
- ✅ Use discard pattern: `_ = SomeMethod();` when return value is intentionally ignored
- ✅ Use range operator: `items[1..^1]` for slicing
- ✅ Use index operator: `items[^1]` for last element

### Naming Conventions
- **Interfaces**: Start with `I` (e.g., `IPackageService`)
- **Classes**: PascalCase (e.g., `PackageService`)
- **Methods/Properties**: PascalCase (e.g., `GetPackageAsync`)
- **Private fields**: `_camelCase` with underscore prefix
- **Parameters/Locals**: `camelCase`
- **Constants**: PascalCase

### Nullable Reference Types
- Codebase uses `#nullable enable` in newer files
- Prefer nullable annotations: `string?` for nullable, `string` for non-nullable
- Use null-forgiving operator `!` sparingly and only when certain

---

## Modern C# 14 Extension Method Patterns

### Core Principles

Extension methods in this codebase should follow modern C# 14 patterns:

1. **Fluent Interface Pattern** - Always return the extended type for method chaining
2. **Collection Expressions** - Use `[]` syntax for all collections
3. **Pattern Matching** - Use `is not null`, switch expressions, and property patterns
4. **Nullable Reference Types** - Use `string?` for optional parameters
5. **Primary Constructors** - When appropriate for simple options classes
6. **Expression-Bodied Members** - For simple one-liner methods

### Extension Method Template

```csharp
namespace AvantiPoint.Packages.Core;

public static class ModernExtensions
{
    // Basic fluent extension
    public static TExtendedType DoSomething<TExtendedType>(
        this TExtendedType instance,
        Action<TExtendedType>? configure = null)
    {
        configure?.Invoke(instance);
        return instance; // Always return for chaining
    }
    
    // With collection expressions
    public static IServiceCollection AddServices(
        this IServiceCollection services)
    {
        // Use collection expressions
        Type[] serviceTypes = [
            typeof(IService1),
            typeof(IService2),
            typeof(IService3)
        ];
        
        foreach (var type in serviceTypes)
        {
            services.AddScoped(type);
        }
        
        return services;
    }
    
    // With pattern matching
    public static IServiceCollection AddStorage(
        this IServiceCollection services,
        string? storageType)
    {
        return storageType switch
        {
            "filesystem" => services.AddScoped<IStorageService, FileStorageService>(),
            "azure" => services.AddScoped<IStorageService, BlobStorageService>(),
            "aws" => services.AddScoped<IStorageService, S3StorageService>(),
            null => throw new ArgumentNullException(nameof(storageType)),
            _ => throw new ArgumentException($"Unknown storage type: {storageType}", nameof(storageType))
        };
    }
    
    // Expression-bodied for simple extensions
    public static string ToNormalizedString(this string value) =>
        value.Trim().ToLowerInvariant();
}
```

### Common Extension Patterns in This Codebase

**DI Registration Extensions:**
```csharp
public static NuGetApiOptions AddFeature(
    this NuGetApiOptions options,
    Action<FeatureOptions>? configure = null)
{
    options.Services.AddNuGetApiOptions<FeatureOptions>("Feature");
    
    if (configure is not null)
    {
        options.Services.Configure(configure);
    }
    
    options.Services.TryAddScoped<IFeatureService, FeatureService>();
    
    return options; // Fluent return
}
```

**WebApplication Extensions:**
```csharp
public static WebApplication MapFeatureApi(this WebApplication app)
{
    app.MapGet("/api/feature", GetFeature)
       .WithTags("Feature")
       .WithName(Routes.FeatureRouteName);
    
    return app; // Fluent return
}
```

**Collection Extensions:**
```csharp
public static T[] ToArray<T>(this IEnumerable<T> source) =>
    source is T[] array ? array : [..source]; // Collection expression with spread
```

---

## Common Patterns

### Dependency Injection

#### Modern C# 14 Extension Methods Pattern

All DI registration uses **fluent extension methods** with modern C# 14 features. Follow these patterns:

**Basic Extension Method (Fluent Pattern):**
```csharp
namespace AvantiPoint.Packages.Core;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddNuGetApiApplication(
        this IServiceCollection services,
        Action<NuGetApiOptions> configureAction)
    {
        var options = new NuGetApiOptions(services);
        configureAction(options);
        return services; // Always return for method chaining
    }
}
```

**Modern Extension Method with Collection Expressions:**
```csharp
public static IServiceCollection AddServices(
    this IServiceCollection services)
{
    // Use collection expressions for service lists
    var serviceTypes = new[]
    {
        typeof(IService1),
        typeof(IService2),
        typeof(IService3)
    };
    
    // Or with collection expressions:
    Type[] serviceTypes = [typeof(IService1), typeof(IService2), typeof(IService3)];
    
    foreach (var serviceType in serviceTypes)
    {
        services.AddScoped(serviceType);
    }
    
    return services;
}
```

**Extension Method with Pattern Matching:**
```csharp
public static IServiceCollection AddStorage(
    this IServiceCollection services,
    string storageType)
{
    return storageType switch
    {
        "filesystem" => services.AddScoped<IStorageService, FileStorageService>(),
        "azure" => services.AddScoped<IStorageService, BlobStorageService>(),
        "aws" => services.AddScoped<IStorageService, S3StorageService>(),
        _ => throw new ArgumentException($"Unknown storage type: {storageType}", nameof(storageType))
    };
}
```

**Extension Method with Nullable Reference Types:**
```csharp
public static IServiceCollection AddOptions<T>(
    this IServiceCollection services,
    string? configurationKey = null)
    where T : class
{
    if (configurationKey is not null)
    {
        services.Configure<T>(options => { /* configure */ });
    }
    else
    {
        services.Configure<T>(options => { /* configure from root */ });
    }
    
    return services;
}
```

**Extension Method with Primary Constructor Pattern (for options):**
```csharp
// For options classes, prefer primary constructors when appropriate
public class StorageOptions(string connectionString)
{
    public string ConnectionString { get; } = connectionString;
    public int TimeoutSeconds { get; init; } = 30;
}

// Extension method using the options
public static IServiceCollection AddStorage(
    this IServiceCollection services,
    StorageOptions options)
{
    services.AddSingleton(options);
    return services;
}
```

**Key Principles for Modern Extension Methods:**
1. ✅ **Always return the extended type** for fluent chaining: `return services;`
2. ✅ **Use file-scoped namespaces** in new extension classes
3. ✅ **Use collection expressions** for lists/arrays
4. ✅ **Use switch expressions** for type-based routing
5. ✅ **Use pattern matching** for null checks and type checks
6. ✅ **Use nullable reference types** (`string?` for optional parameters)
7. ✅ **Use `nameof()`** for parameter names in error messages
8. ✅ **Keep methods focused** - one responsibility per extension method
9. ✅ **Use expression-bodied members** for simple one-liners when appropriate

#### Options Pattern
Configuration options use the standard .NET Options pattern:

```csharp
// In DependencyInjectionExtensions
services.AddNuGetApiOptions<PackageFeedOptions>();
services.AddNuGetApiOptions<DatabaseOptions>(nameof(PackageFeedOptions.Database));

// Options class
public class PackageFeedOptions
{
    public DatabaseOptions Database { get; set; }
    // ...
}

// Usage via IOptions<T> or IOptionsSnapshot<T>
public class MyService
{
    private readonly PackageFeedOptions _options;
    
    public MyService(IOptionsSnapshot<PackageFeedOptions> options)
    {
        _options = options.Value;
    }
}
```

#### Provider Pattern
The codebase uses a custom provider pattern for conditional service registration:

```csharp
// Register a provider
services.AddProvider<IStorageService>((provider, config) =>
{
    if (config.HasStorageType("filesystem"))
        return provider.GetRequiredService<FileStorageService>();
    return null;
});

// Resolve using providers
var service = GetServiceFromProviders<IStorageService>(serviceProvider);
```

**Key Points:**
- Providers check configuration to determine if they should provide a service
- Multiple providers can be registered, first matching one wins
- Used for database, storage, and search service selection

#### Service Lifetime Patterns
- **Singleton**: Stateless services, factories, configuration
- **Scoped**: Database contexts (`IContext`)
- **Transient**: Most business logic services, per-request operations

### Modern Extension Methods for Application Setup

Each provider project uses modern C# 14 extension methods in `*ApplicationExtensions.cs` files:

**Modern Pattern with Collection Expressions and Pattern Matching:**
```csharp
namespace AvantiPoint.Packages;

public static class AzureApplicationExtensions
{
    public static NuGetApiOptions AddAzureBlobStorage(
        this NuGetApiOptions options,
        Action<AzureBlobStorageOptions>? configure = null)
    {
        options.Services.AddNuGetApiOptions<AzureBlobStorageOptions>(
            nameof(PackageFeedOptions.Storage));
        
        if (configure is not null)
        {
            options.Services.Configure(configure);
        }
        
        // Register services using collection expressions where applicable
        options.Services.TryAddScoped<IStorageService, BlobStorageService>();
        
        return options; // Fluent return
    }
    
    // Overload with direct configuration
    public static NuGetApiOptions AddAzureBlobStorage(
        this NuGetApiOptions options,
        string connectionString)
    {
        return options.AddAzureBlobStorage(opts =>
        {
            opts.ConnectionString = connectionString;
        });
    }
}
```

**Modern Usage Patterns:**
```csharp
// Fluent chaining with collection expressions
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage()
           .AddSqlServerDatabase("SqlServer")
           .AddRepositorySigning();
});

// With configuration
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddAzureBlobStorage(opts =>
    {
        opts.ConnectionString = connectionString;
        opts.ContainerName = "packages";
    });
});

// Using switch expression for conditional setup
var storageType = builder.Configuration["Storage:Type"];
builder.Services.AddNuGetPackageApi(options =>
{
    _ = storageType switch
    {
        "azure" => options.AddAzureBlobStorage(),
        "aws" => options.AddS3Storage(),
        "filesystem" => options.AddFileStorage(),
        _ => throw new InvalidOperationException($"Unknown storage type: {storageType}")
    };
});
```

**Best Practices:**
- ✅ Return the extended type for fluent chaining
- ✅ Use nullable action parameters (`Action<T>?`) for optional configuration
- ✅ Provide overloads for common scenarios
- ✅ Use collection expressions for service registration lists
- ✅ Use switch expressions for type-based routing
- ✅ Use `nameof()` for configuration key names

### Modern API Endpoint Pattern (C# 14)

API endpoints use modern C# 14 extension methods with collection expressions and pattern matching:

```csharp
namespace AvantiPoint.Packages.Hosting;

internal static class RepositorySignatures
{
    public static WebApplication MapRepositorySignaturesApi(this WebApplication app)
    {
        app.MapGet("v3/repository-signatures/index.json", GetRepositorySignatures)
           .AllowAnonymous()
           .WithTags(nameof(RepositorySignatures))
           .WithName(Routes.RepositorySignaturesRouteName);
        
        return app; // Fluent return
    }

    // Use primary constructor pattern for simple handlers if appropriate
    // Or use minimal API pattern with dependency injection
    private static async Task<IResult> GetRepositorySignatures(
        RepositorySigningCertificateService service,
        CancellationToken cancellationToken)
    {
        var certificates = await service.GetActiveCertificatesAsync(cancellationToken);
        
        // Use collection expressions for response building
        var response = new RepositorySignaturesResponse
        {
            AllRepositorySigned = certificates.Count > 0,
            Certificates = certificates.Select(c => new RepositoryCertificateInfo
            {
                Fingerprints = new CertificateFingerprints
                {
                    Sha256 = c.Sha256Fingerprint,
                    Sha384 = c.Sha384Fingerprint ?? null, // Null-conditional
                    Sha512 = c.Sha512Fingerprint ?? null
                },
                Subject = c.Subject,
                Issuer = c.Issuer,
                NotBefore = c.NotBefore,
                NotAfter = c.NotAfter
            }).ToList() // Or use collection expression if building from scratch
        };
        
        return Results.Ok(response);
    }
}
```

**Modern Registration with Expression-Bodied Member:**
```csharp
// In PackagesApi.cs - use expression-bodied member for simple mapping
public static WebApplication MapNuGetApiRoutes(this WebApplication app) =>
    app.MapServiceIndex()
       .MapPackageContentRoutes()
       .MapPackageMetadataRoutes()
       .MapPackagePublishRoutes()
       .MapSearchRoutes()
       .MapShieldRoutes()
       .MapSymbolRoutes()
       .MapVulnerabilityApi()
       .MapRepositorySignaturesApi();
```

**Modern Endpoint with Pattern Matching:**
```csharp
private static async Task<IResult> GetPackage(
    string packageId,
    string version,
    IPackageContentService contentService,
    CancellationToken cancellationToken)
{
    var stream = await contentService.GetPackageContentStreamOrNullAsync(
        packageId, 
        NuGetVersion.Parse(version), 
        cancellationToken);
    
    return stream switch
    {
        null => Results.NotFound(),
        var s => Results.File(s, "application/octet-stream", $"{packageId}.{version}.nupkg")
    };
}
```

### Entity Framework Patterns

#### Context Pattern
- Each database provider has: `*Context.cs` (inherits `AbstractContext`), `*ContextFactory.cs`
- `AbstractContext` defines all `DbSet<T>` properties
- Migrations are provider-specific

#### **CRITICAL: Database Migrations Rules**

**ALWAYS use `dotnet ef` tool to generate migrations. NEVER manually create or edit migration files except for data preservation SQL.**

1. **Creating Migrations:**
   - **SQL Server:** `dotnet ef migrations add MigrationName --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations`
   - **SQLite:** `dotnet ef migrations add MigrationName --context SqliteContext --project src/AvantiPoint.Packages.Database.Sqlite --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations`
   - The tool will generate the migration file, Designer file, and update the snapshot automatically

2. **When to Edit Migration Files:**
   - **ONLY** when you need to add SQL queries to preserve existing data during schema changes
   - Example: When consolidating columns, add data migration SQL between adding new columns and dropping old ones
   - Example pattern:
     ```csharp
     // Step 1: Add new columns as nullable
     migrationBuilder.AddColumn<string>(name: "NewColumn", ...);
     
     // Step 2: Migrate data (ONLY edit needed)
     migrationBuilder.Sql(@"UPDATE Table SET NewColumn = OldColumn WHERE OldColumn IS NOT NULL");
     
     // Step 3: Make columns non-nullable
     migrationBuilder.AlterColumn<string>(name: "NewColumn", nullable: false, ...);
     
     // Step 4: Drop old columns
     migrationBuilder.DropColumn(name: "OldColumn", ...);
     ```

3. **What NOT to Do:**
   - ❌ **NEVER** manually create migration files
   - ❌ **NEVER** edit the Designer.cs files
   - ❌ **NEVER** manually edit the ModelSnapshot.cs files
   - ❌ **NEVER** modify the Up() or Down() methods except to add data preservation SQL
   - ❌ **NEVER** change column types, names, or constraints manually - regenerate the migration instead

4. **If You Need to Fix a Migration:**
   - Remove the incorrect migration: `dotnet ef migrations remove --context <ContextName> --project <ProjectPath>`
   - Fix the entity model or configuration
   - Regenerate the migration using `dotnet ef migrations add`

#### **CRITICAL: Database Migrations Rules**

**ALWAYS use `dotnet ef` tool to generate migrations. NEVER manually create or edit migration files except for data preservation SQL.**

1. **Creating Migrations:**
   - **SQL Server:** `dotnet ef migrations add MigrationName --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations`
   - **SQLite:** `dotnet ef migrations add MigrationName --context SqliteContext --project src/AvantiPoint.Packages.Database.Sqlite --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations`
   - The tool will generate the migration file, Designer file, and update the snapshot automatically

2. **When to Edit Migration Files:**
   - **ONLY** when you need to add SQL queries to preserve existing data during schema changes
   - Example: When consolidating columns, add data migration SQL between adding new columns and dropping old ones
   - Example pattern:
     ```csharp
     // Step 1: Add new columns as nullable
     migrationBuilder.AddColumn<string>(name: "NewColumn", ...);
     
     // Step 2: Migrate data (ONLY edit needed)
     migrationBuilder.Sql(@"UPDATE Table SET NewColumn = OldColumn WHERE OldColumn IS NOT NULL");
     
     // Step 3: Make columns non-nullable
     migrationBuilder.AlterColumn<string>(name: "NewColumn", nullable: false, ...);
     
     // Step 4: Drop old columns
     migrationBuilder.DropColumn(name: "OldColumn", ...);
     ```

3. **What NOT to Do:**
   - ❌ **NEVER** manually create migration files
   - ❌ **NEVER** edit the Designer.cs files
   - ❌ **NEVER** manually edit the ModelSnapshot.cs files
   - ❌ **NEVER** modify the Up() or Down() methods except to add data preservation SQL
   - ❌ **NEVER** change column types, names, or constraints manually - regenerate the migration instead

4. **If You Need to Fix a Migration:**
   - Remove the incorrect migration: `dotnet ef migrations remove --context <ContextName> --project <ProjectPath>`
   - Fix the entity model or configuration
   - Regenerate the migration using `dotnet ef migrations add`

#### Entity Pattern
```csharp
namespace AvantiPoint.Packages.Core;

public class Package
{
    public int Key { get; set; }
    public string Id { get; set; }
    // ...
}
```

### Storage Service Pattern

Storage services implement `IStorageService`:

```csharp
public interface IStorageService
{
    Task<Stream> GetAsync(string path, CancellationToken cancellationToken);
    Task PutAsync(string path, Stream content, string contentType, CancellationToken cancellationToken);
    // ...
}
```

Implementations:
- `FileStorageService` - Local file system
- `BlobStorageService` - Azure Blob Storage
- `S3StorageService` - AWS S3

### Authentication Pattern

```csharp
public interface IPackageAuthenticationService
{
    Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);
    Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken);
}
```

Default implementation: `ApiKeyAuthenticationService`

### Callback Pattern

```csharp
public interface INuGetFeedActionHandler
{
    Task OnPackageDownloadedAsync(string packageId, string version, CancellationToken cancellationToken);
    Task OnPackageUploadedAsync(string packageId, string version, CancellationToken cancellationToken);
    // ...
}
```

---

## File Organization Guidelines

### Directory Structure
- Group related files by feature/domain (e.g., `Signing/`, `Storage/`, `Authentication/`)
- Keep interfaces and implementations in the same directory
- Configuration classes in `Configuration/` subdirectory
- Extension methods in `Extensions/` subdirectory
- API endpoints in `Apis/` subdirectory (Hosting project)

### File Naming
- Match file name to primary type name: `PackageService.cs` contains `PackageService`
- Interface files: `IPackageService.cs`
- Options classes: `*Options.cs`
- Extension classes: `*Extensions.cs`
- Context classes: `*Context.cs`

### Partial Classes
- Use partial classes for large classes that need organization
- Example: `DependencyInjectionExtensions.cs` + `DependencyInjectionExtensions.Providers.cs`

---

## Testing Patterns

### Test Organization
- Mirror source structure: `tests/AvantiPoint.Packages.Tests/Signing/` mirrors `src/.../Signing/`
- Test file naming: `*Tests.cs` (e.g., `SigningOptionsTests.cs`)

### Test Patterns
```csharp
public class SigningOptionsTests
{
    [Fact]
    public void Validate_WhenModeIsNull_ReturnsNoErrors()
    {
        // Arrange
        var options = new SigningOptions { Mode = null };
        
        // Act
        var results = options.Validate(context).ToList();
        
        // Assert
        Assert.Empty(results);
    }
}
```

### Mocking
- Use **Moq** for mocking dependencies
- Mock interfaces, not concrete classes
- Use `Mock<T>` for complex scenarios, direct instantiation for simple cases

### Database Testing
- Use **EF Core InMemory** for unit tests
- Use actual database for integration tests (if needed)

---

## Configuration Patterns

### Configuration Keys
Configuration uses hierarchical keys matching property paths:

```json
{
  "PackageFeed": {
    "Database": {
      "Type": "SqlServer",
      "ConnectionString": "..."
    },
    "Storage": {
      "Type": "AzureBlob",
      "ConnectionString": "..."
    },
    "Signing": {
      "Mode": "SelfSigned",
      "SelfSigned": {
        "Organization": "MyOrg",
        "KeySize": 4096
      }
    }
  }
}
```

### Options Validation
Options classes implement `IValidatableObject` for complex validation:

```csharp
public class SigningOptions : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Mode == SigningMode.SelfSigned && SelfSigned == null)
        {
            yield return new ValidationResult("SelfSigned must be configured...");
        }
    }
}
```

---

## Common Tasks and Where to Find Code

### Adding a New API Endpoint
1. Create endpoint class in `src/AvantiPoint.Packages.Hosting/Apis/YourEndpoint.cs`
2. Add route constant to `Routes.cs`
3. Add mapping call in `PackagesApi.MapNuGetApiRoutes()`
4. Add URL generation in `NuGetFeedUrlGenerator.cs` (if needed)
5. Add to service index in `APPackagesServiceIndex.cs` (if needed)

### Adding a New Storage Provider
1. Create project: `src/AvantiPoint.Packages.YourProvider/`
2. Implement `IStorageService`
3. Create `*ApplicationExtensions.cs` with `Add*Storage()` method
4. Create `Configuration/*StorageOptions.cs`
5. Register provider in `DependencyInjectionExtensions.AddDefaultProviders()`

### Adding a New Database Provider
1. Create project: `src/AvantiPoint.Packages.Database.YourDb/`
2. Create `*Context.cs` inheriting `AbstractContext`
3. Create `*ContextFactory.cs`
4. Create `*ApplicationExtensions.cs` with `Add*Database()` method
5. Add EF Core migrations

### Adding Configuration Options
1. Create options class in `Configuration/` directory
2. Add to `PackageFeedOptions` if top-level
3. Register in `DependencyInjectionExtensions.AddConfiguration()`
4. Add validation via `IValidatableObject` if needed

### Adding a New Service
1. Define interface (if needed) in appropriate directory
2. Implement service class
3. Register in `DependencyInjectionExtensions.AddNuGetApiServices()` or appropriate extension method
4. Use `TryAdd*` methods to allow overrides

---

## Important Files to Reference

### Build Configuration
- `Directory.Build.props` - Common MSBuild properties, LangVersion
- `Directory.Packages.props` - Central package management
- `global.json` - .NET SDK version
- `.editorconfig` - Code style rules

### Core Configuration
- `src/AvantiPoint.Packages.Core/Configuration/PackageFeedOptions.cs` - Main configuration class
- `src/AvantiPoint.Packages.Core/Extensions/DependencyInjectionExtensions.cs` - Main DI setup

### Documentation
- `ReadMe.md` - Project overview
- `docs/` - User documentation
- `.github/copilot-instructions.md` - GitHub Copilot context
- `repository-signing-issue.md` - Feature requirements example
- `repository-signing-progress.md` - Implementation tracking example

---

## Code Generation Guidelines (C# 14 Focus)

When generating or modifying code, **prioritize modern C# 14 features**:

### Required for New Code
1. ✅ **File-scoped namespaces**: `namespace AvantiPoint.Packages.Core.Signing;`
2. ✅ **Collection expressions**: Use `[]` instead of `new List<T>()` or `Array.Empty<T>()`
3. ✅ **Primary constructors**: For DI-only classes with no complex initialization
4. ✅ **Pattern matching**: `is not null`, `is string s`, switch expressions
5. ✅ **Nullable reference types**: Use `string?` for nullable, `string` for non-nullable
6. ✅ **Expression-bodied members**: For simple properties and methods
7. ✅ **Fluent extension methods**: Always return the extended type for chaining

### Best Practices
1. **Follow existing patterns** - Look at similar files first, but modernize the syntax
2. **One type per file** - Don't group multiple public types
3. **Add XML documentation** for public APIs
4. **Use async/await** for I/O operations
5. **Include cancellation tokens** in async methods: `CancellationToken cancellationToken = default`
6. **Validate configuration** using `IValidatableObject` when appropriate
7. **Register services** using fluent extension methods
8. **Follow naming conventions** strictly
9. **Use `nameof()`** for property/parameter names in error messages and attributes
10. **Use switch expressions** instead of switch statements when returning values

### Modern C# 14 Examples

**Before (Old Style):**
```csharp
namespace AvantiPoint.Packages.Core
{
    public class MyService
    {
        private readonly ILogger<MyService> _logger;
        private readonly IOptions<MyOptions> _options;
        
        public MyService(ILogger<MyService> logger, IOptions<MyOptions> options)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }
        
        public List<string> GetItems()
        {
            var items = new List<string>();
            if (_options.Value.Items != null)
            {
                items.AddRange(_options.Value.Items);
            }
            return items;
        }
    }
}
```

**After (C# 14 Style):**
```csharp
namespace AvantiPoint.Packages.Core;

public class MyService(ILogger<MyService> logger, IOptions<MyOptions> options)
{
    private readonly ILogger<MyService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly MyOptions _options = options.Value ?? throw new InvalidOperationException("Options not configured");
    
    public string[] GetItems() => _options.Items ?? [];
}
```

### When Creating New Files
- Check if similar functionality exists first
- Place in appropriate directory (match namespace to folder structure)
- Use existing patterns (extension methods, options, providers)
- Add to appropriate DI registration method
- Consider if it needs configuration options

### When Modifying Existing Files
- **Preserve existing style** - Don't refactor unless asked
- **Maintain backward compatibility** - Don't break public APIs
- **Update related files** - Configuration, DI registration, tests
- **Follow existing patterns** - Match the style of surrounding code

---

## Testing Guidelines

### When to Write Tests
- New features should include tests
- Bug fixes should include regression tests
- Configuration validation should be tested
- Complex logic should be unit tested

### Test Structure
- Use xUnit `[Fact]` for single tests, `[Theory]` for parameterized tests
- Follow Arrange-Act-Assert pattern
- Use descriptive test names: `MethodName_Scenario_ExpectedBehavior`
- Group related tests in the same class

### What to Test
- Configuration validation (all error cases)
- Service behavior (success and failure paths)
- Edge cases and error handling
- Integration points (if critical)

---

## Common Pitfalls to Avoid

1. **Don't refactor existing code** unless explicitly requested
2. **Don't change namespace style** in existing files (file-scoped vs traditional)
3. **Don't break public APIs** without discussion
4. **Don't add dependencies** without updating `Directory.Packages.props`
5. **Don't hardcode values** - use configuration
6. **Don't forget cancellation tokens** in async methods
7. **Don't skip validation** for configuration options
8. **Don't create circular dependencies** between projects

---

## Quick Reference: Common Commands

```bash
# Build
dotnet build APPackages.sln

# Restore packages
dotnet restore APPackages.sln

# Run tests
dotnet test APPackages.sln

# Run sample
dotnet run --project samples/OpenFeed/OpenFeed.csproj

# Add EF migration (SQL Server)
dotnet ef migrations add MigrationName --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations

# Add EF migration (SQLite)
dotnet ef migrations add MigrationName --context SqliteContext --project src/AvantiPoint.Packages.Database.Sqlite --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations

# Remove last migration (if needed to fix)
dotnet ef migrations remove --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer

# Add EF migration (SQL Server)
dotnet ef migrations add MigrationName --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations

# Add EF migration (SQLite)
dotnet ef migrations add MigrationName --context SqliteContext --project src/AvantiPoint.Packages.Database.Sqlite --startup-project samples/OpenFeed/OpenFeed.csproj --output-dir Migrations

# Remove last migration (if needed to fix)
dotnet ef migrations remove --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer

# Update database
dotnet ef database update --context SqlServerContext --project src/AvantiPoint.Packages.Database.SqlServer
```

---

## Session Optimization Tips

1. **Start with context** - Read relevant existing files before generating code
2. **Follow patterns** - Match existing code style and structure
3. **Check configuration** - Understand how options are structured
4. **Verify DI registration** - Ensure services are properly registered
5. **Consider testing** - Think about testability when designing code
6. **Use existing abstractions** - Don't create new ones unnecessarily
7. **Reference similar implementations** - Look at comparable features first

---

**Last Updated:** Based on repository state as of latest commit  
**Maintained By:** AI Assistant (Cursor)


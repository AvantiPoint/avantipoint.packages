# AvantiPoint Packages - GitHub Copilot Instructions

## Project Overview

AvantiPoint Packages is a modern .NET NuGet feed solution based on BaGet, providing custom authenticated package feeds with advanced user authentication and callback hooks. The project targets .NET 10.0 and includes support for multiple storage backends (Azure, AWS, SQL Server, SQLite, MySQL).

### Key Components

- **AvantiPoint.Packages.Core**: Core functionality and abstractions
- **AvantiPoint.Packages.Hosting**: ASP.NET Core hosting integration
- **AvantiPoint.Packages.Protocol**: NuGet protocol implementation
- **AvantiPoint.Packages.Azure**: Azure Blob Storage integration
- **AvantiPoint.Packages.Aws**: AWS S3 Storage integration
- **AvantiPoint.Packages.Database.***: Database providers (SQL Server, SQLite, MySQL)

### Sample Applications

- **OpenFeed**: Simple, open NuGet feed without authentication
- **AuthenticatedFeed**: Secured feed with authentication and callbacks

## Build and Test

### Prerequisites

- .NET 10.0 SDK (see `global.json` for exact version requirements)
- Solution uses Central Package Management (see `Directory.Packages.props`)

### Build Commands

```bash
# Restore dependencies
dotnet restore APPackages.sln

# Build the solution
dotnet build APPackages.sln

# Build in Release mode
dotnet build APPackages.sln -c Release
```

### Running Samples

```bash
# Run the OpenFeed sample
dotnet run --project samples/OpenFeed/OpenFeed.csproj

# Run the AuthenticatedFeed sample
dotnet run --project samples/AuthenticatedFeed/AuthenticatedFeed.csproj
```

### Testing

This repository currently does not have a dedicated test project. When making changes, verify by:
1. Building the solution successfully
2. Running the sample applications
3. Testing NuGet operations (push, restore, install) against the sample feeds

## Coding Standards and Conventions

### General Guidelines

- Use C# latest language features (as specified in `Directory.Build.props`)
- Follow standard .NET naming conventions
- Keep code consistent with the existing codebase style
- All public APIs should have XML documentation comments
- Use async/await for I/O operations
- Prefer primary constructors when a class only needs DI dependencies and performs no initialization logic beyond assigning them. Use a traditional constructor only if additional setup, validation, or derived state initialization is required.

### File Organization

- Source code in `src/` directory
- Sample applications in `samples/` directory
- Documentation in `docs/` directory
- Build configuration files at solution root
- One top-level public type per file (class, record, interface, struct). Do not group multiple public types in a single file. Supporting types should be moved to their own files or made nested if truly private to the parent.

### Project Configuration

- Use Central Package Management - add package references to `Directory.Packages.props`
- All projects inherit from `Directory.Build.props` and `Directory.Build.targets`
- Version management handled by Nerdbank.GitVersioning (see `version.json`)

## Authentication and Security

### Important Security Considerations

- **Never commit secrets or API keys** to the repository
- Authentication is handled through `IPackageAuthenticationService` interface
- Two authentication methods supported:
  1. **API Key authentication** (for package publishing)
  2. **Basic authentication** (username + token for package consumers)
- Authentication is separate from ASP.NET Core authentication

### Key Interfaces

- `IPackageAuthenticationService`: Implement for custom user authentication
- `INuGetFeedActionHandler`: Implement for handling upload/download events and callbacks

## Dependencies and Frameworks

### Core Dependencies

- ASP.NET Core (for web hosting)
- Entity Framework Core (for database operations)
- NuGet.Protocol (for NuGet operations)
- Azure Storage / AWS SDK (for cloud storage)

### Package Management

- All package versions are centrally managed in `Directory.Packages.props`
- Use `<PackageReference Include="PackageName" />` without Version attribute in project files
- Add version specifications only to `Directory.Packages.props`

## Database Performance and Optimization

### Query Optimization Guidelines

When working with database queries, follow these best practices to maintain optimal performance:

1. **Avoid N+1 Query Patterns**
   - Always fetch related data in batched queries rather than individual queries
   - Use `GroupBy` and aggregate data before iterating
   - Example: Fetch all versions for multiple packages in one query instead of per-package queries

2. **Use AsNoTracking for Read-Only Queries**
   - Always use `.AsNoTracking()` when reading data that won't be modified
   - This reduces memory overhead and improves query performance

3. **Minimize Included Navigation Properties**
   - Only `Include()` navigation properties that are actually needed
   - Avoid including `PackageDownloads` collection unless absolutely necessary
   - Use separate queries for download counts instead of loading entire collections

4. **Leverage Database Views and Indexes**
   - Database views are available for common aggregations:
     - `vw_PackageDownloadCounts`: Pre-aggregated download counts per package
     - `vw_LatestPackageVersions`: Latest version of each package
     - `vw_PackageSearchInfo`: Package info with pre-calculated download totals
     - `vw_PackageVersionsWithDownloads`: All versions with download counts
   - Indexes exist on frequently filtered columns: `Listed`, `IsPrerelease`, `Published`, `SemVerLevel`

5. **Batch Related Queries**
   - Calculate aggregates (like download counts) separately from main queries
   - Use dictionaries to map results after fetching in bulk
   - Group operations to reduce database round trips

6. **Example Pattern for Optimized Queries**
   ```csharp
   // BAD: N+1 pattern
   foreach (var package in packages)
   {
       var downloads = await _context.PackageDownloads
           .Where(d => d.PackageKey == package.Key)
           .CountAsync();
   }

   // GOOD: Batch query
   var packageKeys = packages.Select(p => p.Key).ToList();
   var downloadCounts = await _context.PackageDownloads
       .Where(d => packageKeys.Contains(d.PackageKey))
       .GroupBy(d => d.PackageKey)
       .Select(g => new { PackageKey = g.Key, Count = g.Count() })
       .ToListAsync();
   var downloadDict = downloadCounts.ToDictionary(x => x.PackageKey, x => x.Count);
   ```

### Adding Database Migrations

When adding new migrations that affect performance:

1. **Create a SINGLE Migration for All Related Changes**
   - **CRITICAL**: Do NOT create separate migrations for related features (e.g., indexes, views, new tables)
   - Always consolidate all schema changes into ONE migration
   - Example: If adding indexes AND views, put them in the SAME migration file
   - This avoids migration clutter and makes rollback simpler

2. **Create Indexes for New Filtered Columns**
   - Add indexes for columns used in `WHERE`, `ORDER BY`, or `JOIN` clauses
   - Consider composite indexes for frequently combined filters

3. **Create Views for Complex Aggregations**
   - If a query performs expensive aggregations repeatedly, create a view
   - Views should be created for all supported databases: SQL Server, SQLite, MySQL
   - Use database-specific syntax (e.g., `FOR JSON PATH` in SQL Server, `json_group_array()` in SQLite)

4. **Migration Process**
   - Create migration for SQL Server: `dotnet ef migrations add <Name> --context SqlServerContext`
   - Create migration for SQLite: `dotnet ef migrations add <Name> --context SqliteContext`
   - Create migration for MySQL: `dotnet ef migrations add <Name> --context MySqlContext` (when provider supports current EF version)
   - Ensure `Down` migration properly removes views and indexes in reverse order

5. **View Creation Syntax Differences**
   - SQL Server: Use `[dbo].[viewname]` and `ISNULL()`, supports `FOR JSON PATH`
   - SQLite: Use `viewname` (no schema) and `COALESCE()`, use `json_group_array()` for JSON
   - MySQL: Similar to SQL Server but with backticks for identifiers

6. **Using Views in Application Code**
   - Create a read-only entity class (e.g., `PackageWithJsonData`) to map to the view
   - Configure the entity in `AbstractContext.OnModelCreating()` using `.ToView("view_name")`
   - Add the DbSet to AbstractContext (e.g., `public DbSet<PackageWithJsonData> PackagesWithJsonData { get; set; }`)
   - Query the view instead of using multiple `Include()` statements
   - Example: Use `_context.PackagesWithJsonData.AsNoTracking()` instead of `_context.Packages.Include().Include().Include()`

### Performance Monitoring

- Monitor DTU usage in Azure SQL databases, especially for CI-heavy feeds
- Watch for slow queries in application logs
- Review query execution plans for complex searches
- Consider read replicas for high-traffic scenarios

## Common Tasks

### Adding a New Package Dependency

1. Add package reference to `Directory.Packages.props` with version
2. Add `<PackageReference Include="PackageName" />` to relevant `.csproj` files
3. Build and test to ensure compatibility

### Adding a New Storage Provider

1. Create new project in `src/` directory following naming pattern: `AvantiPoint.Packages.<Provider>`
2. Implement required storage interfaces from `AvantiPoint.Packages.Core`
3. Add dependency injection extensions for service registration
4. Update solution file and documentation

### Modifying Authentication Logic

1. Changes to authentication should be made in `AvantiPoint.Packages.Core`
2. Update both sample projects to demonstrate new authentication features
3. Document breaking changes in commit messages

## Documentation

- Main documentation is in the `docs/` directory (MkDocs format)
- Documentation site configuration in `mkdocs.yml`
- Update documentation when adding new features or changing public APIs
- README.md should contain getting started information

## CI/CD

- Build workflow: `.github/workflows/build-packages.yml`
- Documentation workflow: `.github/workflows/docs.yml`
- Uses reusable workflows from `avantipoint/workflow-templates`
- Builds triggered on push to `master` and pull requests
- NuGet packages deployed to internal feed on successful builds

## Tips for Working with This Repository

- When modifying core functionality, test against both sample applications
- Check that changes don't break existing authentication implementations
- Ensure database migrations are compatible with all supported database providers
- Consider backward compatibility when changing public APIs
- Update documentation site for user-facing changes

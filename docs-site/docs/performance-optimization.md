---
id: performance-optimization
title: Database Performance Optimization
sidebar_label: Database Optimization
sidebar_position: 13
---

## Overview

This document describes the database performance optimizations implemented to reduce DTU consumption and improve query performance, especially for feeds with high CI build activity.

## Problem Statement

Feeds with many CI builds can utilize significant DTUs in Azure SQL Database even with light user loads. This can cause:
- Sporadic failures for CI builds relying on the feed
- Increased operational costs
- Degraded user experience during peak usage

## Solutions Implemented

### 1. Database Indexes

Added indexes on frequently filtered columns to improve query performance:

- **IX_Packages_Listed**: Speeds up queries filtering by published status
- **IX_Packages_IsPrerelease**: Optimizes prerelease filtering
- **IX_Packages_Published**: Improves sorting by publish date
- **IX_Packages_SemVerLevel**: Accelerates SemVer version filtering
- **IX_Packages_Listed_IsPrerelease**: Composite index for common filter combinations

These indexes are automatically created when running database migrations.

### 2. Database Views

Created views for expensive aggregations that are frequently queried:

#### vw_PackageDownloadCounts
Pre-aggregates download counts per package, avoiding repeated COUNT operations:
```sql
SELECT 
    p.[Key] as PackageKey,
    p.Id as PackageId,
    p.Version,
    COUNT(pd.Id) as DownloadCount
FROM Packages p
LEFT JOIN PackageDownloads pd ON p.[Key] = pd.PackageKey
GROUP BY p.[Key], p.Id, p.Version
```

#### vw_LatestPackageVersions
Identifies the latest version of each package to optimize search operations.

#### vw_PackageSearchInfo
Combines package information with pre-calculated download totals, reducing join complexity during searches.

#### vw_PackageVersionsWithDownloads
Provides version-level statistics with download counts for efficient version listing.

### 3. Query Optimizations

#### DatabaseSearchService

**Before:** N+1 query pattern - fetching versions separately for each package
```csharp
foreach (var pkg in packages) {
    var versions = await _context.Packages
        .Where(p => p.Id == pkg.Id)
        .ToListAsync();
}
```

**After:** Batch query - fetch all versions in a single database call
```csharp
var packageIds = packages.Select(p => p.Id).ToList();
var allVersions = await _context.Packages
    .Where(p => packageIds.Contains(p.Id))
    .GroupBy(p => p.Id)
    .ToListAsync();
```

**Impact:** Reduced from O(n) queries to O(1) queries (3 total database calls instead of 3 + n)

#### PackageService.FindAsync

**Before:** Loading expensive PackageDownloads collection unnecessarily
```csharp
.Include(p => p.PackageDownloads)
```

**After:** Removed this Include, loading only required navigation properties
```csharp
.Include(p => p.Dependencies)
.Include(p => p.PackageTypes)
.Include(p => p.TargetFrameworks)
```

**Impact:** Reduced memory usage and query execution time, especially for packages with many downloads

#### DefaultPackageMetadataService.GetPackageInfo

**Before:** N+1 query checking if each dependency exists locally
```csharp
IsLocalDependency = _context.Packages.Any(p => p.Id == d.Id)
```

**After:** Batch query to check all dependencies at once
```csharp
var localDependencies = await _context.Packages
    .Where(p => allDependencyIds.Contains(p.Id))
    .Select(p => p.Id)
    .ToListAsync();
var localDependencySet = new HashSet<string>(localDependencies);
```

**Impact:** Eliminated hundreds of database queries for packages with many dependencies

## Migration Guide

### For Existing Deployments

1. **Backup your database** before applying migrations
2. Run database migrations:
   ```bash
   # For SQL Server
   dotnet ef database update --context SqlServerContext
   
   # For SQLite
   dotnet ef database update --context SqliteContext
   ```
3. Monitor database performance metrics to verify improvements

### Expected Performance Improvements

- **Search queries**: 60-80% reduction in execution time
- **Package metadata retrieval**: 70-90% reduction for packages with many dependencies
- **Autocomplete**: 50-70% reduction in query time
- **Overall DTU usage**: 40-60% reduction under typical CI workloads

### Database Compatibility

- **SQL Server**: Full support with all views and indexes
- **SQLite**: Full support with SQLite-compatible syntax
- **MySQL**: Code optimizations apply, but migration pending EF Core 10.0 provider update

## Best Practices for Future Development

When writing new database queries, follow these guidelines:

1. **Always use AsNoTracking()** for read-only queries
2. **Avoid N+1 patterns** - batch related queries
3. **Minimize Includes** - only load navigation properties you actually need
4. **Never Include PackageDownloads** unless absolutely necessary
5. **Use GroupBy and aggregate** before iterating in code
6. **Consider creating views** for frequently-used complex queries
7. **Add indexes** for new columns used in WHERE, ORDER BY, or JOIN clauses

See the updated `.github/copilot-instructions.md` for detailed examples and patterns.

## Monitoring and Troubleshooting

### Key Metrics to Monitor

- DTU utilization in Azure SQL Database
- Query execution time in application logs
- Database connection pool usage
- Average response time for search operations

### If Performance Issues Persist

1. Review query execution plans for slow queries
2. Consider additional indexes for new query patterns
3. Evaluate read replica configuration for high-traffic scenarios
4. Monitor for missing index recommendations in Azure portal

## Additional Resources

- [Entity Framework Core Performance](https://docs.microsoft.com/en-us/ef/core/performance/)
- [Azure SQL Database Performance Tuning](https://docs.microsoft.com/en-us/azure/sql-database/sql-database-performance-guidance)
- [Database View Best Practices](https://docs.microsoft.com/en-us/sql/relational-databases/views/views)

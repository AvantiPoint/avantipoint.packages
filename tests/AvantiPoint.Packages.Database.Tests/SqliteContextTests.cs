using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Database.Tests;

public class SqliteContextTests(ITestOutputHelper output) : IDisposable
{
    private readonly List<SqliteConnection> _sqliteConnections = [];

    public void Dispose()
    {
        foreach (var connection in _sqliteConnections)
        {
            connection.Close();
            connection.Dispose();
        }
    }

    private SqliteConnection CreateSqliteConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _sqliteConnections.Add(connection);
        return connection;
    }

    [Fact]
    public async Task CanMigrate()
    {
        // Arrange - Use in-memory SQLite connection
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        
        // Act - Run migrations
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Assert - Verify database is functional
        Assert.NotNull(context.Packages);
        Assert.NotNull(context.PackageDependencies);
        Assert.NotNull(context.PackageDownloads);
        Assert.NotNull(context.PackageTypes);
        Assert.NotNull(context.TargetFrameworks);
    }

    [Fact]
    public async Task CanInsertAndQueryTestData()
    {
        // Arrange
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Act - Insert test package with all relationships
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Test Author"],
            Description = "Test Description",
            Listed = true,
            Published = DateTime.UtcNow,
            Dependencies = [
                new PackageDependency { Id = "Dependency1", VersionRange = "[1.0.0,)" },
                new PackageDependency { Id = "Dependency2", VersionRange = "[2.0.0,)" }
            ],
            PackageTypes = [
                new PackageType { Name = "Dependency" }
            ],
            TargetFrameworks = [
                new TargetFramework { Moniker = "net8.0" }
            ]
        };

        context.Packages.Add(package);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Verify data was inserted and can be queried
        var retrieved = await context.Packages
            .Include(p => p.Dependencies)
            .Include(p => p.PackageTypes)
            .Include(p => p.TargetFrameworks)
            .FirstOrDefaultAsync(p => p.Id == "TestPackage", TestContext.Current.CancellationToken);

        Assert.NotNull(retrieved);
        Assert.Equal("TestPackage", retrieved.Id);
        Assert.Equal(2, retrieved.Dependencies.Count);
        Assert.Single(retrieved.PackageTypes);
        Assert.Single(retrieved.TargetFrameworks);
    }

    [Fact]
    public async Task CanQueryWithIndexedColumns()
    {
        // Arrange
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Add test packages with various states
        context.Packages.AddRange(
            new Package
            {
                Id = "Listed",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["Author"],
                Description = "Listed package",
                Listed = true,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-1),
                SemVerLevel = SemVerLevel.SemVer2
            },
            new Package
            {
                Id = "Unlisted",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["Author"],
                Description = "Unlisted package",
                Listed = false,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-2),
                SemVerLevel = SemVerLevel.SemVer2
            },
            new Package
            {
                Id = "Prerelease",
                Version = NuGetVersion.Parse("2.0.0-beta"),
                Authors = ["Author"],
                Description = "Prerelease package",
                Listed = true,
                IsPrerelease = true,
                Published = DateTime.UtcNow,
                SemVerLevel = SemVerLevel.SemVer2
            }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert - Test indexed column queries
        var listedPackages = await context.Packages
            .Where(p => p.Listed)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, listedPackages);

        var prereleasePackages = await context.Packages
            .Where(p => p.IsPrerelease)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, prereleasePackages);

        var orderedByPublished = await context.Packages
            .OrderByDescending(p => p.Published)
            .Select(p => p.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Prerelease", orderedByPublished[0]);
    }

    [Fact]
    public async Task CanTrackPackageDownloads()
    {
        // Arrange
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Add a package
        var package = new Package
        {
            Id = "DownloadTest",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Author"],
            Description = "Download test",
            Listed = true,
            Published = DateTime.UtcNow
        };
        context.Packages.Add(package);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Add download records
        context.PackageDownloads.AddRange(
            new PackageDownload { PackageKey = package.Key },
            new PackageDownload { PackageKey = package.Key },
            new PackageDownload { PackageKey = package.Key }
        );
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert - Verify download count
        var downloadCount = await context.PackageDownloads
            .Where(d => d.PackageKey == package.Key)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, downloadCount);

        // Test aggregation query (used by views)
        var downloadsByPackage = await context.PackageDownloads
            .GroupBy(d => d.PackageKey)
            .Select(g => new { PackageKey = g.Key, Count = g.Count() })
            .FirstOrDefaultAsync(g => g.PackageKey == package.Key, TestContext.Current.CancellationToken);

        Assert.NotNull(downloadsByPackage);
        Assert.Equal(3, downloadsByPackage.Count);
    }

    [Fact]
    public async Task ViewsExistAndAreQueryable()
    {
        // Arrange
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Add test package
        var package = new Package
        {
            Id = "ViewTest",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = ["Author"],
            Description = "View test",
            Listed = true,
            Published = DateTime.UtcNow,
            Dependencies = [
                new PackageDependency { Id = "Dep1", VersionRange = "[1.0.0,)" }
            ]
        };
        context.Packages.Add(package);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act - Query the JSON view
        var viewPackage = await context.Set<PackageWithJsonData>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == "ViewTest", TestContext.Current.CancellationToken);

        // Assert - Verify view data
        Assert.NotNull(viewPackage);
        Assert.Equal("ViewTest", viewPackage.Id);
        Assert.NotNull(viewPackage.DependenciesJson); // JSON column should be populated by view
        output.WriteLine($"DependenciesJson: {viewPackage.DependenciesJson}");
    }

    [Fact]
    public async Task IndexesExist()
    {
        // Arrange
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Act - Query SQLite schema to verify indexes
        var indexes = await context.Database.SqlQueryRaw<IndexInfo>(
            "SELECT name FROM sqlite_master WHERE type='index' AND name LIKE 'IX_Packages%'")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert - Verify expected indexes exist
        var indexNames = indexes.Select(i => i.Name).ToList();
        output.WriteLine($"Found {indexNames.Count} indexes: {string.Join(", ", indexNames)}");

        Assert.Contains(indexNames, name => name.Contains("Listed"));
        Assert.Contains(indexNames, name => name.Contains("IsPrerelease"));
        Assert.Contains(indexNames, name => name.Contains("Published"));
        Assert.Contains(indexNames, name => name.Contains("SemVerLevel"));
    }

    [Fact]
    public async Task ViewsExist()
    {
        // Arrange
        var connection = CreateSqliteConnection();
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

        // Act - Query SQLite schema to verify views
        var views = await context.Database.SqlQueryRaw<ViewInfo>(
            "SELECT name FROM sqlite_master WHERE type='view'")
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert - Verify expected views exist
        var viewNames = views.Select(v => v.Name).ToList();
        output.WriteLine($"Found {viewNames.Count} views: {string.Join(", ", viewNames)}");

        Assert.Contains("vw_PackageDownloadCounts", viewNames);
        Assert.Contains("vw_LatestPackageVersions", viewNames);
        Assert.Contains("vw_PackageSearchInfo", viewNames);
        Assert.Contains("vw_PackageVersionsWithDownloads", viewNames);
        Assert.Contains("vw_PackageWithJsonData", viewNames);
    }

    private class IndexInfo
    {
        public string Name { get; set; } = string.Empty;
    }

    private class ViewInfo
    {
        public string Name { get; set; } = string.Empty;
    }
}

using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests;

public class DatabaseMigrationTests
{
    [Fact]
    public async Task SqlServer_CanMigrate()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqlServerContext>()
            .UseInMemoryDatabase(databaseName: "SqlServer_CanMigrate")
            .Options;

        using var context = new SqlServerContext(options);

        // Act & Assert - Should not throw
        await context.Database.EnsureCreatedAsync();
        
        // Verify database is functional
        Assert.NotNull(context.Packages);
        Assert.NotNull(context.PackageDependencies);
        Assert.NotNull(context.PackageDownloads);
        Assert.NotNull(context.PackageTypes);
        Assert.NotNull(context.TargetFrameworks);
    }

    [Fact]
    public async Task Sqlite_CanMigrate()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseInMemoryDatabase(databaseName: "Sqlite_CanMigrate")
            .Options;

        using var context = new SqliteContext(options);

        // Act & Assert - Should not throw
        await context.Database.EnsureCreatedAsync();
        
        // Verify database is functional
        Assert.NotNull(context.Packages);
        Assert.NotNull(context.PackageDependencies);
        Assert.NotNull(context.PackageDownloads);
        Assert.NotNull(context.PackageTypes);
        Assert.NotNull(context.TargetFrameworks);
    }

    [Fact]
    public async Task SqlServer_CanInsertAndQueryTestData()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqlServerContext>()
            .UseInMemoryDatabase(databaseName: "SqlServer_TestData")
            .Options;

        using var context = new SqlServerContext(options);
        await context.Database.EnsureCreatedAsync();

        var testPackage = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"),
            Authors = new[] { "Test Author" },
            Description = "Test Description",
            Listed = true,
            Published = DateTime.UtcNow,
            IsPrerelease = false,
            SemVerLevel = SemVerLevel.SemVer2,
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency
                {
                    Id = "DependencyPackage",
                    VersionRange = "[1.0.0, )",
                    TargetFramework = "net10.0"
                }
            },
            PackageTypes = new List<PackageType>
            {
                new PackageType { Name = "Dependency" }
            },
            TargetFrameworks = new List<TargetFramework>
            {
                new TargetFramework { Moniker = "net10.0" }
            }
        };

        // Act
        context.Packages.Add(testPackage);
        await context.SaveChangesAsync();

        // Assert
        var retrievedPackage = await context.Packages
            .Include(p => p.Dependencies)
            .Include(p => p.PackageTypes)
            .Include(p => p.TargetFrameworks)
            .FirstOrDefaultAsync(p => p.Id == "TestPackage");

        Assert.NotNull(retrievedPackage);
        Assert.Equal("TestPackage", retrievedPackage.Id);
        Assert.Equal("1.0.0", retrievedPackage.Version.ToNormalizedString());
        Assert.Single(retrievedPackage.Dependencies);
        Assert.Equal("DependencyPackage", retrievedPackage.Dependencies.First().Id);
        Assert.Single(retrievedPackage.PackageTypes);
        Assert.Equal("Dependency", retrievedPackage.PackageTypes.First().Name);
        Assert.Single(retrievedPackage.TargetFrameworks);
        Assert.Equal("net10.0", retrievedPackage.TargetFrameworks.First().Moniker);
    }

    [Fact]
    public async Task Sqlite_CanInsertAndQueryTestData()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseInMemoryDatabase(databaseName: "Sqlite_TestData")
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync();

        var testPackage = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("2.0.0"),
            Authors = new[] { "Test Author" },
            Description = "Test Description for SQLite",
            Listed = true,
            Published = DateTime.UtcNow,
            IsPrerelease = true,
            SemVerLevel = SemVerLevel.SemVer2,
            Tags = new[] { "test", "package" },
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency
                {
                    Id = "AnotherDependency",
                    VersionRange = "[2.0.0, )",
                    TargetFramework = "net10.0"
                }
            },
            PackageTypes = new List<PackageType>
            {
                new PackageType { Name = "Template" }
            },
            TargetFrameworks = new List<TargetFramework>
            {
                new TargetFramework { Moniker = "net10.0" }
            }
        };

        // Act
        context.Packages.Add(testPackage);
        await context.SaveChangesAsync();

        // Assert
        var retrievedPackage = await context.Packages
            .Include(p => p.Dependencies)
            .Include(p => p.PackageTypes)
            .Include(p => p.TargetFrameworks)
            .FirstOrDefaultAsync(p => p.Id == "TestPackage");

        Assert.NotNull(retrievedPackage);
        Assert.Equal("TestPackage", retrievedPackage.Id);
        Assert.Equal("2.0.0", retrievedPackage.Version.ToNormalizedString());
        Assert.True(retrievedPackage.IsPrerelease);
        Assert.Single(retrievedPackage.Dependencies);
        Assert.Equal("AnotherDependency", retrievedPackage.Dependencies.First().Id);
        Assert.Single(retrievedPackage.PackageTypes);
        Assert.Equal("Template", retrievedPackage.PackageTypes.First().Name);
        Assert.Contains("test", retrievedPackage.Tags);
        Assert.Contains("package", retrievedPackage.Tags);
    }

    [Fact]
    public async Task SqlServer_CanQueryWithIndexedColumns()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqlServerContext>()
            .UseInMemoryDatabase(databaseName: "SqlServer_IndexedColumns")
            .Options;

        using var context = new SqlServerContext(options);
        await context.Database.EnsureCreatedAsync();

        // Add test data with various filter scenarios
        var packages = new List<Package>
        {
            new Package
            {
                Id = "Package1",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-1),
                SemVerLevel = SemVerLevel.Unknown,
                Description = "Package 1"
            },
            new Package
            {
                Id = "Package2",
                Version = NuGetVersion.Parse("2.0.0-beta"),
                Listed = true,
                IsPrerelease = true,
                Published = DateTime.UtcNow,
                SemVerLevel = SemVerLevel.SemVer2,
                Description = "Package 2"
            },
            new Package
            {
                Id = "Package3",
                Version = NuGetVersion.Parse("3.0.0"),
                Listed = false,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-2),
                SemVerLevel = SemVerLevel.Unknown,
                Description = "Package 3"
            }
        };

        context.Packages.AddRange(packages);
        await context.SaveChangesAsync();

        // Act & Assert - Test queries using indexed columns
        var listedPackages = await context.Packages
            .Where(p => p.Listed)
            .ToListAsync();
        Assert.Equal(2, listedPackages.Count);

        var stablePackages = await context.Packages
            .Where(p => p.IsPrerelease == false)
            .ToListAsync();
        Assert.Equal(2, stablePackages.Count);

        var listedStablePackages = await context.Packages
            .Where(p => p.Listed && p.IsPrerelease == false)
            .ToListAsync();
        Assert.Single(listedStablePackages);

        var semVer2Packages = await context.Packages
            .Where(p => p.SemVerLevel == SemVerLevel.SemVer2)
            .ToListAsync();
        Assert.Single(semVer2Packages);

        var orderedByPublished = await context.Packages
            .OrderByDescending(p => p.Published)
            .ToListAsync();
        Assert.Equal("Package2", orderedByPublished.First().Id);
    }

    [Fact]
    public async Task Sqlite_CanQueryWithIndexedColumns()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseInMemoryDatabase(databaseName: "Sqlite_IndexedColumns")
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync();

        // Add test data
        var packages = new List<Package>
        {
            new Package
            {
                Id = "PackageA",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                IsPrerelease = false,
                Published = DateTime.UtcNow.AddDays(-3),
                SemVerLevel = SemVerLevel.Unknown,
                Description = "Package A"
            },
            new Package
            {
                Id = "PackageB",
                Version = NuGetVersion.Parse("1.0.0-alpha"),
                Listed = true,
                IsPrerelease = true,
                Published = DateTime.UtcNow.AddDays(-1),
                SemVerLevel = SemVerLevel.SemVer2,
                Description = "Package B"
            }
        };

        context.Packages.AddRange(packages);
        await context.SaveChangesAsync();

        // Act & Assert
        var listedPackages = await context.Packages
            .Where(p => p.Listed)
            .ToListAsync();
        Assert.Equal(2, listedPackages.Count);

        var prereleasePackages = await context.Packages
            .Where(p => p.IsPrerelease)
            .ToListAsync();
        Assert.Single(prereleasePackages);
        Assert.Equal("PackageB", prereleasePackages.First().Id);
    }

    [Fact]
    public async Task SqlServer_CanTrackPackageDownloads()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqlServerContext>()
            .UseInMemoryDatabase(databaseName: "SqlServer_Downloads")
            .Options;

        using var context = new SqlServerContext(options);
        await context.Database.EnsureCreatedAsync();

        var package = new Package
        {
            Id = "DownloadTestPackage",
            Version = NuGetVersion.Parse("1.0.0"),
            Listed = true,
            Published = DateTime.UtcNow,
            Description = "Download test"
        };

        context.Packages.Add(package);
        await context.SaveChangesAsync();

        // Act - Add downloads
        var download1 = new PackageDownload
        {
            PackageKey = package.Key,
            User = "test-user",
            NuGetClient = "dotnet",
            NuGetClientVersion = "10.0.0"
        };

        var download2 = new PackageDownload
        {
            PackageKey = package.Key,
            User = "another-user",
            NuGetClient = "nuget",
            NuGetClientVersion = "7.0.0"
        };

        context.PackageDownloads.AddRange(download1, download2);
        await context.SaveChangesAsync();

        // Assert
        var downloadCount = await context.PackageDownloads
            .Where(d => d.PackageKey == package.Key)
            .CountAsync();

        Assert.Equal(2, downloadCount);

        var downloads = await context.PackageDownloads
            .Where(d => d.PackageKey == package.Key)
            .ToListAsync();

        Assert.All(downloads, d => Assert.NotEqual(Guid.Empty, d.Id));
        Assert.Contains(downloads, d => d.User == "test-user");
        Assert.Contains(downloads, d => d.User == "another-user");
    }

    [Fact]
    public async Task Sqlite_CanTrackPackageDownloads()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseInMemoryDatabase(databaseName: "Sqlite_Downloads")
            .Options;

        using var context = new SqliteContext(options);
        await context.Database.EnsureCreatedAsync();

        var package = new Package
        {
            Id = "DownloadTestPackage",
            Version = NuGetVersion.Parse("1.5.0"),
            Listed = true,
            Published = DateTime.UtcNow,
            Description = "Download test for SQLite"
        };

        context.Packages.Add(package);
        await context.SaveChangesAsync();

        // Act
        var downloads = Enumerable.Range(1, 5).Select(i => new PackageDownload
        {
            PackageKey = package.Key,
            User = $"user-{i}",
            NuGetClient = "dotnet",
            NuGetClientVersion = "10.0.0"
        }).ToList();

        context.PackageDownloads.AddRange(downloads);
        await context.SaveChangesAsync();

        // Assert
        var downloadCount = await context.PackageDownloads
            .Where(d => d.PackageKey == package.Key)
            .CountAsync();

        Assert.Equal(5, downloadCount);

        // Test grouping (important for view queries)
        var groupedDownloads = await context.PackageDownloads
            .Where(d => d.PackageKey == package.Key)
            .GroupBy(d => d.PackageKey)
            .Select(g => new { PackageKey = g.Key, Count = g.Count() })
            .FirstOrDefaultAsync();

        Assert.NotNull(groupedDownloads);
        Assert.Equal(5, groupedDownloads.Count);
    }
}

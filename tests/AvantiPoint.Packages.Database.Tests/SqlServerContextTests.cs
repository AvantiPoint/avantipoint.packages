using System;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Database.Tests;

public class SqlServerContextTests(SqlServerTestcontainerFixture fixture, ITestOutputHelper output) : IClassFixture<SqlServerTestcontainerFixture>
{
    private async Task WithNewContext(Func<SqlServerContext, Task> test)
    {
        var handle = await fixture.CreateDatabaseAsync();

        try
        {
            var options = new DbContextOptionsBuilder<SqlServerContext>()
                .UseSqlServer(handle.ConnectionString)
                .Options;

            await using var context = new SqlServerContext(options);
            await context.Database.MigrateAsync();

            await test(context);
        }
        finally
        {
            await fixture.DropDatabaseAsync(handle.DatabaseName);
        }
    }

    [DockerFact]
    public Task CanMigrate()
    {
        return WithNewContext(context =>
        {
            Assert.NotNull(context.Packages);
            Assert.NotNull(context.PackageDependencies);
            Assert.NotNull(context.PackageDownloads);
            Assert.NotNull(context.PackageTypes);
            Assert.NotNull(context.TargetFrameworks);
            return Task.CompletedTask;
        });
    }

    [DockerFact]
    public async Task CanInsertAndQueryTestData()
    {
        await WithNewContext(async context =>
        {
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
            await context.SaveChangesAsync();

            var retrieved = await context.Packages
                .Include(p => p.Dependencies)
                .Include(p => p.PackageTypes)
                .Include(p => p.TargetFrameworks)
                .FirstOrDefaultAsync(p => p.Id == "TestPackage");

            Assert.NotNull(retrieved);
            Assert.Equal("TestPackage", retrieved.Id);
            Assert.Equal(2, retrieved.Dependencies.Count);
            Assert.Single(retrieved.PackageTypes);
            Assert.Single(retrieved.TargetFrameworks);
        });
    }

    [DockerFact]
    public async Task CanQueryWithIndexedColumns()
    {
        await WithNewContext(async context =>
        {
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
            await context.SaveChangesAsync();

            // Act & Assert - Test indexed column queries
            var listedPackages = await context.Packages
                .Where(p => p.Listed)
                .CountAsync();
            Assert.Equal(2, listedPackages);

            var prereleasePackages = await context.Packages
                .Where(p => p.IsPrerelease)
                .CountAsync();
            Assert.Equal(1, prereleasePackages);

            var orderedByPublished = await context.Packages
                .OrderByDescending(p => p.Published)
                .Select(p => p.Id)
                .ToListAsync();
            Assert.Equal("Prerelease", orderedByPublished[0]);
        });
    }

    [DockerFact]
    public async Task CanTrackPackageDownloads()
    {
        await WithNewContext(async context =>
        {
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
            await context.SaveChangesAsync();

            context.PackageDownloads.AddRange(
                new PackageDownload { PackageKey = package.Key },
                new PackageDownload { PackageKey = package.Key },
                new PackageDownload { PackageKey = package.Key }
            );
            await context.SaveChangesAsync();

            var downloadCount = await context.PackageDownloads
                .Where(d => d.PackageKey == package.Key)
                .CountAsync();
            Assert.Equal(3, downloadCount);

            // Test aggregation query (used by views)
            var downloadsByPackage = await context.PackageDownloads
                .GroupBy(d => d.PackageKey)
                .Select(g => new { PackageKey = g.Key, Count = g.Count() })
                .FirstOrDefaultAsync(g => g.PackageKey == package.Key);

            Assert.NotNull(downloadsByPackage);
            Assert.Equal(3, downloadsByPackage.Count);
        });
    }

    [DockerFact]
    public async Task ViewsExistAndAreQueryable()
    {
        await WithNewContext(async context =>
        {
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
            await context.SaveChangesAsync();

            var viewPackage = await context.Set<PackageWithJsonData>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == "ViewTest");

            // Assert - Verify view data
            Assert.NotNull(viewPackage);
            Assert.Equal("ViewTest", viewPackage.Id);
            Assert.NotNull(viewPackage.DependenciesJson);
            output.WriteLine($"DependenciesJson: {viewPackage.DependenciesJson}");
        });
    }

    [DockerFact]
    public async Task IndexesExist()
    {
        await WithNewContext(async context =>
        {
            var indexes = await context.Database.SqlQueryRaw<IndexInfo>(
                @"SELECT name FROM sys.indexes 
                  WHERE object_id = OBJECT_ID('dbo.Packages') 
                  AND name LIKE 'IX_Packages%'")
                .ToListAsync();

            // Assert - Verify expected indexes exist
            var indexNames = indexes.Select(i => i.Name).ToList();
            output.WriteLine($"Found {indexNames.Count} indexes: {string.Join(", ", indexNames)}");

            Assert.Contains(indexNames, name => name.Contains("Listed"));
            Assert.Contains(indexNames, name => name.Contains("IsPrerelease"));
            Assert.Contains(indexNames, name => name.Contains("Published"));
            Assert.Contains(indexNames, name => name.Contains("SemVerLevel"));
        });
    }

    [DockerFact]
    public async Task ViewsExist()
    {
        await WithNewContext(async context =>
        {
            var views = await context.Database.SqlQueryRaw<ViewInfo>(
                "SELECT name FROM sys.views")
                .ToListAsync();

            // Assert - Verify expected views exist
            var viewNames = views.Select(v => v.Name).ToList();
            output.WriteLine($"Found {viewNames.Count} views: {string.Join(", ", viewNames)}");

            Assert.Contains("vw_PackageDownloadCounts", viewNames);
            Assert.Contains("vw_LatestPackageVersions", viewNames);
            Assert.Contains("vw_PackageSearchInfo", viewNames);
            Assert.Contains("vw_PackageVersionsWithDownloads", viewNames);
            Assert.Contains("vw_PackageWithJsonData", viewNames);
        });
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

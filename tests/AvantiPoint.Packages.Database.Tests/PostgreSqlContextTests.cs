using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.PostgreSql;
using AvantiPoint.Packages.Database.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Database.Tests;

public class PostgreSqlContextTests(PostgreSqlTestcontainerFixture fixture, ITestOutputHelper output) : IClassFixture<PostgreSqlTestcontainerFixture>
{
    private async Task WithNewContext(Func<PostgreSqlContext, Task> test)
    {
        var handle = await fixture.CreateDatabaseAsync();
        try
        {
            var options = new DbContextOptionsBuilder<PostgreSqlContext>()
                .UseNpgsql(handle.ConnectionString)
                .Options;

            await using var context = new PostgreSqlContext(options);
            await context.Database.MigrateAsync(TestContext.Current.CancellationToken);

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
    public Task CanInsertAndQueryTestData()
    {
        return WithNewContext(async context =>
        {
            var package = new Package
            {
                Id = "PgPackage",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["Test Author"],
                Description = "Postgres Package",
                Listed = true,
                Published = DateTime.UtcNow,
                Dependencies =
                [
                    new PackageDependency { Id = "Dependency1", VersionRange = "[1.0.0,)" },
                    new PackageDependency { Id = "Dependency2", VersionRange = "[2.0.0,)" }
                ],
                PackageTypes = [new PackageType { Name = "Library" }],
                TargetFrameworks = [new TargetFramework { Moniker = "net8.0" }]
            };

            context.Packages.Add(package);
            await context.SaveChangesAsync();

            var retrieved = await context.Packages
                .Include(p => p.Dependencies)
                .Include(p => p.PackageTypes)
                .Include(p => p.TargetFrameworks)
                .FirstOrDefaultAsync(p => p.Id == "PgPackage");

            Assert.NotNull(retrieved);
            Assert.Equal(2, retrieved.Dependencies.Count);
            Assert.Single(retrieved.PackageTypes);
            Assert.Single(retrieved.TargetFrameworks);
        });
    }

    [DockerFact]
    public Task CanQueryWithIndexedColumns()
    {
        return WithNewContext(async context =>
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
                });
            await context.SaveChangesAsync();

            Assert.Equal(2, await context.Packages.Where(p => p.Listed).CountAsync());
            Assert.Equal(1, await context.Packages.Where(p => p.IsPrerelease).CountAsync());

            var ordered = await context.Packages
                .OrderByDescending(p => p.Published)
                .Select(p => p.Id)
                .ToListAsync();

            Assert.Equal("Prerelease", ordered.First());
        });
    }

    [DockerFact]
    public Task CanTrackPackageDownloads()
    {
        return WithNewContext(async context =>
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
                new PackageDownload { PackageKey = package.Key });
            await context.SaveChangesAsync();

            var count = await context.PackageDownloads.Where(d => d.PackageKey == package.Key).CountAsync();
            Assert.Equal(3, count);

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
    public Task ViewsExistAndAreQueryable()
    {
        return WithNewContext(async context =>
        {
            var package = new Package
            {
                Id = "ViewTest",
                Version = NuGetVersion.Parse("1.0.0"),
                Authors = ["Author"],
                Description = "View test",
                Listed = true,
                Published = DateTime.UtcNow,
                Dependencies =
                [
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
    public Task IndexesExist()
    {
        return WithNewContext(async context =>
        {
            var indexes = await context.Database.SqlQueryRaw<IndexInfo>(
                @"SELECT indexname as Name FROM pg_indexes 
                  WHERE tablename = 'Packages' 
                  AND indexname LIKE 'IX_Packages%'")
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
    public Task ViewsExist()
    {
        return WithNewContext(async context =>
        {
            var views = await context.Database.SqlQueryRaw<ViewInfo>(
                @"SELECT table_name as Name FROM information_schema.views 
                  WHERE table_schema = 'public'")
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


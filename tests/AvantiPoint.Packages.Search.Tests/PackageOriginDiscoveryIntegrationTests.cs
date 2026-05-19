using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Search.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Search.Tests;

public sealed class PackageOriginDiscoveryIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;

    public PackageOriginDiscoveryIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SqliteContext(options);
        _context.Database.EnsureCreated();

        _context.Packages.AddRange(
            new Package
            {
                Id = "Published.Only",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                Published = DateTime.UtcNow,
                Origin = PackageOrigin.Published,
            },
            new Package
            {
                Id = "Mirrored.Package",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                Published = DateTime.UtcNow,
                Origin = PackageOrigin.Mirrored,
            });

        _context.SaveChanges();
    }

    [Fact]
    public async Task Search_WithIncludeMirroredPackagesFalse_ReturnsOnlyPublished()
    {
        var search = CreateSearchService(includeMirrored: false);

        var response = await search.SearchAsync(
            new SearchRequest
            {
                Query = null,
                Take = 20,
                Skip = 0,
                IncludePrerelease = true,
                IncludeSemVer2 = true,
            },
            CancellationToken.None);

        Assert.Equal(1, response.TotalHits);
        Assert.Single(response.Data);
        Assert.Equal("Published.Only", response.Data[0].PackageId);
    }

    [Fact]
    public async Task Search_WithIncludeMirroredPackagesFalse_ExcludesMirroredVersionsFromSamePackageId()
    {
        _context.Packages.AddRange(
            new Package
            {
                Id = "Mixed.Origin",
                Version = NuGetVersion.Parse("1.0.0"),
                Listed = true,
                Published = DateTime.UtcNow.AddDays(-1),
                Origin = PackageOrigin.Published,
            },
            new Package
            {
                Id = "Mixed.Origin",
                Version = NuGetVersion.Parse("2.0.0"),
                Listed = true,
                Published = DateTime.UtcNow,
                Origin = PackageOrigin.Mirrored,
            });
        await _context.SaveChangesAsync();

        var search = CreateSearchService(includeMirrored: false);

        var response = await search.SearchAsync(
            new SearchRequest
            {
                Query = "Mixed",
                Take = 20,
                Skip = 0,
                IncludePrerelease = true,
                IncludeSemVer2 = true,
            },
            CancellationToken.None);

        Assert.Equal(1, response.TotalHits);
        var hit = Assert.Single(response.Data);
        Assert.Equal("Mixed.Origin", hit.PackageId);
        Assert.Single(hit.Versions);
        Assert.Equal("1.0.0", hit.Versions[0].Version);
    }

    [Fact]
    public async Task Search_WithIncludeMirroredPackagesTrue_ExcludesCachedOnly()
    {
        _context.Packages.Add(new Package
        {
            Id = "Cached.Only",
            Version = NuGetVersion.Parse("1.0.0"),
            Listed = true,
            Published = DateTime.UtcNow,
            Origin = PackageOrigin.Cached,
        });
        await _context.SaveChangesAsync();

        var search = CreateSearchService(includeMirrored: true);

        var response = await search.SearchAsync(
            new SearchRequest
            {
                Query = null,
                Take = 20,
                Skip = 0,
                IncludePrerelease = true,
                IncludeSemVer2 = true,
            },
            CancellationToken.None);

        Assert.Equal(2, response.TotalHits);
        Assert.DoesNotContain(response.Data, p => p.PackageId == "Cached.Only");
    }

    private DatabaseSearchService CreateSearchService(bool includeMirrored)
    {
        return new DatabaseSearchService(
            _context,
            new FrameworkCompatibilityService(),
            new TestUrlGenerator(),
            Options.Create(new SearchOptions { IncludeMirroredPackages = includeMirrored }));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

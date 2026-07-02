using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Maintenance;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests.Maintenance;

public class RetentionPolicyServiceTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 07, 01, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly Mock<IPackageDeletionService> _deletion = new();

    public RetentionPolicyServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task KeepsNewestPrereleases_PrunesOlderOnes()
    {
        SeedPackage("MyApp", "1.0.0-pre.1", prerelease: true, publishedDaysAgo: 30);
        SeedPackage("MyApp", "1.0.0-pre.2", prerelease: true, publishedDaysAgo: 20);
        SeedPackage("MyApp", "1.0.0-pre.3", prerelease: true, publishedDaysAgo: 10);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(new RetentionOptions
        {
            Enabled = true,
            MaxPrereleaseVersionsPerPackage = 2,
        });

        var candidates = await service.GetCandidatesAsync(TestContext.Current.CancellationToken);

        var candidate = Assert.Single(candidates);
        Assert.Equal("MyApp", candidate.PackageId);
        Assert.Equal(NuGetVersion.Parse("1.0.0-pre.1"), candidate.Version);
    }

    [Fact]
    public async Task PrunesPrereleasesOlderThanMaxAge()
    {
        SeedPackage("MyApp", "1.0.0-old.1", prerelease: true, publishedDaysAgo: 100);
        SeedPackage("MyApp", "1.0.0-new.1", prerelease: true, publishedDaysAgo: 5);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(new RetentionOptions
        {
            Enabled = true,
            MaxPrereleaseAgeDays = 30,
        });

        var candidates = await service.GetCandidatesAsync(TestContext.Current.CancellationToken);

        var candidate = Assert.Single(candidates);
        Assert.Equal(NuGetVersion.Parse("1.0.0-old.1"), candidate.Version);
    }

    [Fact]
    public async Task NeverPrunesStableMirroredOrExcludedPackages()
    {
        SeedPackage("Stable", "1.0.0", prerelease: false, publishedDaysAgo: 400);
        SeedPackage("Mirrored", "1.0.0-pre.1", prerelease: true, publishedDaysAgo: 400, origin: PackageOrigin.Mirrored);
        SeedPackage("Pinned", "1.0.0-pre.1", prerelease: true, publishedDaysAgo: 400);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(new RetentionOptions
        {
            Enabled = true,
            MaxPrereleaseAgeDays = 30,
            MaxPrereleaseVersionsPerPackage = 0,
            ExcludedPackageIds = ["pinned"], // case-insensitive
        });

        var candidates = await service.GetCandidatesAsync(TestContext.Current.CancellationToken);

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task Disabled_ReturnsNoCandidates()
    {
        SeedPackage("MyApp", "1.0.0-pre.1", prerelease: true, publishedDaysAgo: 400);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(new RetentionOptions
        {
            Enabled = false,
            MaxPrereleaseAgeDays = 30,
        });

        Assert.Empty(await service.GetCandidatesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DryRun_DeletesNothing()
    {
        SeedPackage("MyApp", "1.0.0-pre.1", prerelease: true, publishedDaysAgo: 400);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(new RetentionOptions
        {
            Enabled = true,
            MaxPrereleaseAgeDays = 30,
            DryRun = true,
        });

        var removed = await service.ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, removed);
        _deletion.Verify(
            d => d.TryDeletePackageAsync(It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Apply_DeletesCandidates()
    {
        SeedPackage("MyApp", "1.0.0-pre.1", prerelease: true, publishedDaysAgo: 400);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _deletion
            .Setup(d => d.TryDeletePackageAsync("MyApp", NuGetVersion.Parse("1.0.0-pre.1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(new RetentionOptions
        {
            Enabled = true,
            MaxPrereleaseAgeDays = 30,
        });

        var removed = await service.ApplyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, removed);
        _deletion.VerifyAll();
    }

    private RetentionPolicyService CreateService(RetentionOptions options)
    {
        var snapshot = new Mock<IOptionsSnapshot<RetentionOptions>>();
        snapshot.SetupGet(s => s.Value).Returns(options);

        var time = new Mock<TimeProvider>();
        time.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(Now));

        return new RetentionPolicyService(
            _context,
            _deletion.Object,
            snapshot.Object,
            time.Object,
            Mock.Of<ILogger<RetentionPolicyService>>());
    }

    private void SeedPackage(
        string id,
        string version,
        bool prerelease,
        int publishedDaysAgo,
        PackageOrigin origin = PackageOrigin.Published)
    {
        _context.Packages.Add(new Package
        {
            Id = id,
            Version = NuGetVersion.Parse(version),
            IsPrerelease = prerelease,
            Listed = true,
            Published = Now.AddDays(-publishedDaysAgo),
            Origin = origin,
            Authors = ["test"],
        });
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

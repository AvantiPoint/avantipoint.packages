using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Tests;

public sealed class PackageServiceFeedScopeTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;

    public PackageServiceFeedScopeTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SqliteContext(options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task HardDeletePackageAsync_DeletesOnlyCurrentFeedPackage()
    {
        var version = NuGetVersion.Parse("1.0.0");
        _context.Packages.AddRange(
            CreatePackage("other-feed", "Shared.Package", version),
            CreatePackage(FeedConstants.DefaultFeedId, "Shared.Package", version));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = new PackageService(
            _context,
            new HttpContextAccessor(),
            CreateFeedScope(FeedConstants.DefaultFeedId));

        var deleted = await service.HardDeletePackageAsync(
            "Shared.Package",
            version,
            TestContext.Current.CancellationToken);

        Assert.True(deleted);
        Assert.False(await _context.Packages.AnyAsync(
            p => p.FeedId == FeedConstants.DefaultFeedId && p.Id == "Shared.Package",
            TestContext.Current.CancellationToken));
        Assert.True(await _context.Packages.AnyAsync(
            p => p.FeedId == "other-feed" && p.Id == "Shared.Package",
            TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static IFeedScope CreateFeedScope(string feedId)
    {
        var feedScope = new Mock<IFeedScope>();
        feedScope.Setup(s => s.FeedId).Returns(feedId);
        return feedScope.Object;
    }

    private static Package CreatePackage(string feedId, string id, NuGetVersion version)
    {
        return new Package
        {
            FeedId = feedId,
            Id = id,
            Version = version,
            Listed = true,
            Published = DateTime.UtcNow,
            Origin = PackageOrigin.Published,
        };
    }
}

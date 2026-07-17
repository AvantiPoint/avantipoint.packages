using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Tests.Publishers;

public sealed class NuGetDownstreamPublisherTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;

    public NuGetDownstreamPublisherTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task PushAsync_WithNoVersion_PromotesHighestSemanticVersion_NotMostRecentlyPublished()
    {
        // 1.0.1 is pushed (as a backport) after 2.0.0 already exists; the highest version, 2.0.0,
        // must still be the one promoted.
        AddPackage("Some.Package", "2.0.0", publishedDaysAgo: 5);
        AddPackage("Some.Package", "1.0.1", publishedDaysAgo: 1);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var downstream = new Mock<IDownstreamPublishService>();
        downstream
            .Setup(d => d.PushPackageAsync("Some.Package", It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        downstream
            .Setup(d => d.PushSymbolsAsync("Some.Package", It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var publisher = new NuGetDownstreamPublisher(_context, downstream.Object);
        var target = new HostPublishTarget { Name = "nuget-org", Protocol = PublishTargetProtocol.NuGet };

        var pushed = await publisher.PushAsync(
            new DownstreamPublishRequest("Some.Package"),
            target,
            TestContext.Current.CancellationToken);

        Assert.True(pushed);
        downstream.Verify(
            d => d.PushPackageAsync("Some.Package", NuGetVersion.Parse("2.0.0"), target, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PushAsync_WithExplicitVersion_UsesThatVersion()
    {
        AddPackage("Some.Package", "1.0.0", publishedDaysAgo: 1);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var downstream = new Mock<IDownstreamPublishService>();
        downstream
            .Setup(d => d.PushPackageAsync("Some.Package", NuGetVersion.Parse("1.0.0"), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        downstream
            .Setup(d => d.PushSymbolsAsync("Some.Package", NuGetVersion.Parse("1.0.0"), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var publisher = new NuGetDownstreamPublisher(_context, downstream.Object);
        var target = new HostPublishTarget { Name = "nuget-org", Protocol = PublishTargetProtocol.NuGet };

        var pushed = await publisher.PushAsync(
            new DownstreamPublishRequest("Some.Package", "1.0.0"),
            target,
            TestContext.Current.CancellationToken);

        Assert.True(pushed);
    }

    [Fact]
    public async Task PushAsync_NoMatchingPackage_ReturnsFalse()
    {
        var publisher = new NuGetDownstreamPublisher(_context, Mock.Of<IDownstreamPublishService>(MockBehavior.Strict));
        var target = new HostPublishTarget { Name = "nuget-org", Protocol = PublishTargetProtocol.NuGet };

        var pushed = await publisher.PushAsync(
            new DownstreamPublishRequest("Does.Not.Exist"),
            target,
            TestContext.Current.CancellationToken);

        Assert.False(pushed);
    }

    private void AddPackage(string id, string version, int publishedDaysAgo)
    {
        _context.Packages.Add(new Package
        {
            Id = id,
            Version = NuGetVersion.Parse(version),
            Listed = true,
            Published = DateTime.UtcNow.AddDays(-publishedDaysAgo),
            Authors = ["test"],
        });
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

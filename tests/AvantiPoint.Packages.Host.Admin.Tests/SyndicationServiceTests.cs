using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Tests;

public sealed class SyndicationServiceTests : IDisposable
{
    private readonly SqliteConnection _packageConnection;
    private readonly SqliteConnection _identityConnection;
    private readonly SqliteContext _packageContext;
    private readonly HostSqliteContext _identityContext;

    public SyndicationServiceTests()
    {
        _packageConnection = new SqliteConnection("DataSource=:memory:");
        _packageConnection.Open();
        _packageContext = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(_packageConnection).Options);
        _packageContext.Database.EnsureCreated();

        _identityConnection = new SqliteConnection("DataSource=:memory:");
        _identityConnection.Open();
        _identityContext = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(_identityConnection).Options);
        _identityContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task PushToSourceAsync_ReportsFailures_WhenDownstreamPushFails()
    {
        SeedPackage("Good.Package", "1.0.0");
        SeedPackage("Bad.Package", "2.0.0");
        var target = await SeedGroupAndTargetAsync("mygroup", "nuget-org", "Good.Package", "Bad.Package");

        var downstream = new Mock<IDownstreamPublishService>();
        downstream
            .Setup(d => d.PushPackageAsync("Good.Package", It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        downstream
            .Setup(d => d.PushSymbolsAsync("Good.Package", It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // no symbols - must not fail the package
        downstream
            .Setup(d => d.PushPackageAsync("Bad.Package", It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // upload genuinely fails

        var service = CreateService(downstream.Object);

        var result = await service.PushToSourceAsync("mygroup", "nuget-org", TestContext.Current.CancellationToken);

        Assert.False(result.AllSucceeded);
        Assert.Equal(["Good.Package"], result.PushedPackageIds);
        Assert.Equal(["Bad.Package"], result.FailedPackageIds);
        downstream.Verify(
            d => d.PushSymbolsAsync("Bad.Package", It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()),
            Times.Never); // symbols are never attempted after a failed package push
    }

    [Fact]
    public async Task PushToSourceAsync_AllSucceed_ReportsSuccess()
    {
        SeedPackage("Good.Package", "1.0.0");
        await SeedGroupAndTargetAsync("mygroup", "nuget-org", "Good.Package");

        var downstream = new Mock<IDownstreamPublishService>();
        downstream
            .Setup(d => d.PushPackageAsync(It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        downstream
            .Setup(d => d.PushSymbolsAsync(It.IsAny<string>(), It.IsAny<NuGetVersion>(), It.IsAny<HostPublishTarget>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService(downstream.Object);

        var result = await service.PushToSourceAsync("mygroup", "nuget-org", TestContext.Current.CancellationToken);

        Assert.True(result.AllSucceeded);
        Assert.Equal(["Good.Package"], result.PushedPackageIds);
        Assert.Empty(result.FailedPackageIds);
    }

    [Fact]
    public async Task PushToSourceAsync_MissingLocalPackage_CountsAsFailed()
    {
        // Member added to the group, but no matching row in the package catalog.
        await SeedGroupAndTargetAsync("mygroup", "nuget-org", "Ghost.Package");

        var downstream = new Mock<IDownstreamPublishService>(MockBehavior.Strict);
        var service = CreateService(downstream.Object);

        var result = await service.PushToSourceAsync("mygroup", "nuget-org", TestContext.Current.CancellationToken);

        Assert.False(result.AllSucceeded);
        Assert.Equal(["Ghost.Package"], result.FailedPackageIds);
    }

    [Fact]
    public async Task PushToSourceAsync_UnknownGroup_Throws()
    {
        var service = CreateService(Mock.Of<IDownstreamPublishService>(MockBehavior.Strict));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PushToSourceAsync("does-not-exist", "nuget-org", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PushToSourceAsync_UnknownTarget_Throws()
    {
        _identityContext.HostPackageGroups.Add(new HostPackageGroup { Name = "mygroup" });
        await _identityContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(Mock.Of<IDownstreamPublishService>(MockBehavior.Strict));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PushToSourceAsync("mygroup", "does-not-exist", TestContext.Current.CancellationToken));
    }

    private SyndicationService CreateService(IDownstreamPublishService downstreamPublishService) =>
        new(
            _identityContext,
            _packageContext,
            Mock.Of<IPackageStorageService>(),
            Mock.Of<ISymbolStorageService>(),
            downstreamPublishService);

    private void SeedPackage(string id, string version)
    {
        _packageContext.Packages.Add(new Package
        {
            Id = id,
            Version = NuGetVersion.Parse(version),
            Listed = true,
            Published = DateTime.UtcNow,
            Authors = ["test"],
        });
        _packageContext.SaveChanges();
    }

    private async Task<HostPublishTarget> SeedGroupAndTargetAsync(string groupName, string targetName, params string[] packageIds)
    {
        var target = new HostPublishTarget
        {
            Name = targetName,
            PublishEndpoint = "https://api.nuget.org/v3/index.json",
            ApiToken = "protected-token",
            AddedBy = "test",
            Timestamp = DateTimeOffset.UtcNow,
        };
        _identityContext.HostPublishTargets.Add(target);

        var group = new HostPackageGroup { Name = groupName };
        _identityContext.HostPackageGroups.Add(group);

        foreach (var packageId in packageIds)
        {
            _identityContext.HostPackageGroupMembers.Add(new HostPackageGroupMember
            {
                PackageGroupName = groupName,
                PackageId = packageId,
            });
        }

        await _identityContext.SaveChangesAsync();
        return target;
    }

    public void Dispose()
    {
        _packageContext.Dispose();
        _packageConnection.Dispose();
        _identityContext.Dispose();
        _identityConnection.Dispose();
    }
}

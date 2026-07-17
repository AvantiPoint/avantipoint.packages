using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using AvantiPoint.Packages.Host.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Host.Admin.Tests;

public sealed class SyndicationServiceTests : IDisposable
{
    private readonly SqliteConnection _identityConnection;
    private readonly HostSqliteContext _identityContext;

    public SyndicationServiceTests()
    {
        _identityConnection = new SqliteConnection("DataSource=:memory:");
        _identityConnection.Open();
        _identityContext = new HostSqliteContext(new DbContextOptionsBuilder<HostSqliteContext>().UseSqlite(_identityConnection).Options);
        _identityContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task PushToSourceAsync_ReportsFailures_WhenPublisherFails()
    {
        await SeedGroupAndTargetAsync("mygroup", "nuget-org", PublishTargetProtocol.NuGet, "Good.Package", "Bad.Package");

        var publisher = new FakePublisher(PublishTargetProtocol.NuGet, packageId => packageId != "Bad.Package");
        var service = CreateService([publisher]);

        var result = await service.PushToSourceAsync("mygroup", "nuget-org", TestContext.Current.CancellationToken);

        Assert.False(result.AllSucceeded);
        Assert.Equal(["Good.Package"], result.PushedPackageIds);
        Assert.Equal(["Bad.Package"], result.FailedPackageIds);
    }

    [Fact]
    public async Task PushToSourceAsync_AllSucceed_ReportsSuccess()
    {
        await SeedGroupAndTargetAsync("mygroup", "nuget-org", PublishTargetProtocol.NuGet, "Good.Package");

        var publisher = new FakePublisher(PublishTargetProtocol.NuGet, _ => true);
        var service = CreateService([publisher]);

        var result = await service.PushToSourceAsync("mygroup", "nuget-org", TestContext.Current.CancellationToken);

        Assert.True(result.AllSucceeded);
        Assert.Equal(["Good.Package"], result.PushedPackageIds);
        Assert.Empty(result.FailedPackageIds);
    }

    [Fact]
    public async Task PushToSourceAsync_UnknownGroup_Throws()
    {
        var service = CreateService([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PushToSourceAsync("does-not-exist", "nuget-org", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PushToSourceAsync_UnknownTarget_Throws()
    {
        _identityContext.HostPackageGroups.Add(new HostPackageGroup { Name = "mygroup" });
        await _identityContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PushToSourceAsync("mygroup", "does-not-exist", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PushToSourceAsync_NoPublisherRegisteredForProtocol_Throws()
    {
        await SeedGroupAndTargetAsync("mygroup", "npm-registry", PublishTargetProtocol.Npm, "some-package");

        // Only a NuGet publisher is registered; the target is npm.
        var service = CreateService([new FakePublisher(PublishTargetProtocol.NuGet, _ => true)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.PushToSourceAsync("mygroup", "npm-registry", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SyndicatePackageAsync_SkipsTargetsForOtherProtocols()
    {
        const string packageId = "Shared.Package";
        await SeedGroupAndTargetAsync("mygroup", "nuget-org", PublishTargetProtocol.NuGet, packageId);
        _identityContext.HostPublishTargets.Add(new HostPublishTarget
        {
            Name = "npm-registry",
            PublishEndpoint = "https://registry.npmjs.org",
            Protocol = PublishTargetProtocol.Npm,
            ApiToken = "token",
            AddedBy = "test",
            Timestamp = DateTimeOffset.UtcNow,
        });
        _identityContext.HostPackageGroupSyndications.Add(new HostPackageGroupSyndication
        {
            PackageGroupName = "mygroup",
            PublishTargetName = "npm-registry",
        });
        await _identityContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var downstream = new Mock<IDownstreamPublishService>();
        downstream
            .Setup(d => d.PushPackageAsync(packageId, It.IsAny<NuGetVersion>(), It.Is<HostPublishTarget>(t => t.Name == "nuget-org"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new SyndicationService(
            _identityContext,
            downstream.Object,
            [],
            Mock.Of<IHostEventService>(),
            Mock.Of<ILogger<SyndicationService>>());

        await service.SyndicatePackageAsync(packageId, NuGetVersion.Parse("1.0.0"), TestContext.Current.CancellationToken);

        downstream.Verify(
            d => d.PushPackageAsync(packageId, It.IsAny<NuGetVersion>(), It.Is<HostPublishTarget>(t => t.Name == "nuget-org"), It.IsAny<CancellationToken>()),
            Times.Once);
        downstream.Verify(
            d => d.PushPackageAsync(packageId, It.IsAny<NuGetVersion>(), It.Is<HostPublishTarget>(t => t.Name == "npm-registry"), It.IsAny<CancellationToken>()),
            Times.Never); // the npm target must not receive a NuGet publish request
    }

    [Theory]
    [InlineData(FeedProtocol.Npm, PublishTargetProtocol.Npm)]
    [InlineData(FeedProtocol.Oci, PublishTargetProtocol.Oci)]
    public async Task SyndicateArtifactAsync_PushesMatchingTargetWithSourceSurface(
        FeedProtocol feedProtocol,
        PublishTargetProtocol targetProtocol)
    {
        const string artifactName = "sample/artifact";
        await SeedGroupAndTargetAsync("mygroup", "external", targetProtocol, artifactName);

        var publisher = new Mock<IDownstreamPublisher>();
        publisher.SetupGet(value => value.Protocol).Returns(targetProtocol);
        publisher.Setup(value => value.PushAsync(
                It.IsAny<DownstreamPublishRequest>(),
                It.IsAny<HostPublishTarget>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService([publisher.Object]);
        var surface = new SurfaceContext(
            "feed-a",
            feedProtocol,
            "surface-a",
            feedProtocol == FeedProtocol.Oci ? "docker" : null,
            "/feed",
            new Uri("https://feed.example.test/"));
        var context = new FeedArtifactEventContext(surface, artifactName, "1.2.3", "digest");

        await service.SyndicateArtifactAsync(context, TestContext.Current.CancellationToken);

        publisher.Verify(value => value.PushAsync(
                It.Is<DownstreamPublishRequest>(request =>
                    request.ArtifactName == artifactName
                    && request.Version == "1.2.3"
                    && request.SourceSurface == surface),
                It.Is<HostPublishTarget>(target => target.Name == "external"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private SyndicationService CreateService(IReadOnlyList<IDownstreamPublisher> publishers) =>
        new(
            _identityContext,
            Mock.Of<IDownstreamPublishService>(),
            publishers,
            Mock.Of<IHostEventService>(),
            Mock.Of<ILogger<SyndicationService>>());

    private async Task SeedGroupAndTargetAsync(
        string groupName,
        string targetName,
        PublishTargetProtocol protocol,
        params string[] packageIds)
    {
        _identityContext.HostPublishTargets.Add(new HostPublishTarget
        {
            Name = targetName,
            PublishEndpoint = "https://example.test",
            Protocol = protocol,
            ApiToken = "protected-token",
            AddedBy = "test",
            Timestamp = DateTimeOffset.UtcNow,
        });

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

        _identityContext.HostPackageGroupSyndications.Add(new HostPackageGroupSyndication
        {
            PackageGroupName = groupName,
            PublishTargetName = targetName,
        });

        await _identityContext.SaveChangesAsync();
    }

    private sealed class FakePublisher(PublishTargetProtocol protocol, Func<string, bool> succeeds) : IDownstreamPublisher
    {
        public PublishTargetProtocol Protocol { get; } = protocol;

        public Task<bool> PushAsync(
            DownstreamPublishRequest request,
            HostPublishTarget target,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(succeeds(request.ArtifactName));
    }

    public void Dispose()
    {
        _identityContext.Dispose();
        _identityConnection.Dispose();
    }
}

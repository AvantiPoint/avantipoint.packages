using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Events;

public sealed class CompositeFeedActionHandlerTests
{
    [Fact]
    public async Task CanAccessArtifact_RealDenyIsNotBypassed_ByARegisteredAuditHandler()
    {
        // Regression test: IProtocolNeutralFeedActionHandler implementations (e.g. audit logging)
        // always abstain by returning true from CanAccessArtifact. Because the composite previously
        // OR'd every handler's answer together, registering one alongside a real access-control
        // handler made the whole composite return true unconditionally, silently bypassing a deny.
        var denyHandler = new DenyingHandler();
        var auditHandler = new AuditEventFeedActionHandler(Mock.Of<IHostEventService>(MockBehavior.Strict));
        var composite = new CompositeFeedActionHandler([auditHandler, denyHandler]);

        var context = CreateContext();

        Assert.False(await composite.CanAccessArtifact(context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessArtifact_AllowsAccess_WhenOnlyProtocolNeutralHandlersAreRegistered()
    {
        var auditHandler = new AuditEventFeedActionHandler(Mock.Of<IHostEventService>(MockBehavior.Strict));
        var composite = new CompositeFeedActionHandler([auditHandler]);

        var context = CreateContext();

        Assert.True(await composite.CanAccessArtifact(context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnArtifactUploaded_InvokesEveryHandler_IncludingProtocolNeutralOnes()
    {
        var eventService = new Mock<IHostEventService>();
        var auditHandler = new AuditEventFeedActionHandler(eventService.Object);
        var denyHandler = new DenyingHandler();
        var composite = new CompositeFeedActionHandler([auditHandler, denyHandler]);

        var context = CreateContext(FeedProtocol.Npm);

        await composite.OnArtifactUploaded(context, TestContext.Current.CancellationToken);

        Assert.True(denyHandler.UploadNotified);
        eventService.Verify(
            s => s.RecordAsync("package.published", "some-package", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static FeedArtifactEventContext CreateContext(FeedProtocol protocol = FeedProtocol.NuGet)
    {
        var surface = new SurfaceContext("default", protocol, "surface-id", null, "/v2", new Uri("https://packages.example.com"));
        return new FeedArtifactEventContext(surface, "some-package", "1.0.0", DigestOrTarballPath: null);
    }

    /// <summary>A real access-control handler (like the NuGet adapter) that explicitly denies.</summary>
    private sealed class DenyingHandler : IFeedActionHandler
    {
        public bool UploadNotified { get; private set; }

        public Task<bool> CanAccessArtifact(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task OnArtifactDownloaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task OnArtifactUploaded(FeedArtifactEventContext context, CancellationToken cancellationToken = default)
        {
            UploadNotified = true;
            return Task.CompletedTask;
        }
    }
}

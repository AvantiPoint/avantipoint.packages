using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Host.Admin.Services.Events;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Events;

public sealed class AuditEventFeedActionHandlerTests
{
    [Theory]
    [InlineData(FeedProtocol.Npm)]
    [InlineData(FeedProtocol.Oci)]
    public async Task OnArtifactUploaded_RecordsEvent_ForNonNuGetProtocols(FeedProtocol protocol)
    {
        var eventService = new Mock<IHostEventService>();
        var handler = new AuditEventFeedActionHandler(eventService.Object);
        var context = CreateContext(protocol, version: "1.0.0");

        await handler.OnArtifactUploaded(context, TestContext.Current.CancellationToken);

        eventService.Verify(
            s => s.RecordAsync(
                "package.published",
                "some-package",
                It.Is<string>(d => d!.Contains(protocol.ToString()) && d.Contains("1.0.0")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnArtifactUploaded_DoesNothing_ForNuGet()
    {
        // NuGet uploads are already recorded by HostNuGetFeedActionHandler via the NuGet-specific
        // adapter; recording them here too would duplicate the audit event.
        var eventService = new Mock<IHostEventService>(MockBehavior.Strict);
        var handler = new AuditEventFeedActionHandler(eventService.Object);
        var context = CreateContext(FeedProtocol.NuGet, version: "1.0.0");

        await handler.OnArtifactUploaded(context, TestContext.Current.CancellationToken);

        eventService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task OnArtifactUploaded_DoesNothing_WhenVersionIsMissing()
    {
        var eventService = new Mock<IHostEventService>(MockBehavior.Strict);
        var handler = new AuditEventFeedActionHandler(eventService.Object);
        var context = CreateContext(FeedProtocol.Npm, version: null);

        await handler.OnArtifactUploaded(context, TestContext.Current.CancellationToken);

        eventService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CanAccessArtifact_AlwaysAbstains()
    {
        var handler = new AuditEventFeedActionHandler(Mock.Of<IHostEventService>(MockBehavior.Strict));
        var context = CreateContext(FeedProtocol.Npm, version: "1.0.0");

        Assert.True(await handler.CanAccessArtifact(context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OnArtifactDownloaded_DoesNothing()
    {
        var eventService = new Mock<IHostEventService>(MockBehavior.Strict);
        var handler = new AuditEventFeedActionHandler(eventService.Object);
        var context = CreateContext(FeedProtocol.Npm, version: "1.0.0");

        await handler.OnArtifactDownloaded(context, TestContext.Current.CancellationToken);

        eventService.VerifyNoOtherCalls();
    }

    private static FeedArtifactEventContext CreateContext(FeedProtocol protocol, string? version)
    {
        var surface = new SurfaceContext("default", protocol, "surface-id", null, "/v2", new Uri("https://packages.example.com"));
        return new FeedArtifactEventContext(surface, "some-package", version, DigestOrTarballPath: null);
    }
}

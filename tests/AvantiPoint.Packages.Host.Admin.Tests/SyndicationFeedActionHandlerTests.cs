using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Host.Admin.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests;

public sealed class SyndicationFeedActionHandlerTests
{
    [Theory]
    [InlineData(FeedProtocol.Npm)]
    [InlineData(FeedProtocol.Oci)]
    public async Task OnArtifactUploaded_ForRegistryProtocol_InvokesSyndication(FeedProtocol protocol)
    {
        var service = new Mock<ISyndicationService>();
        var handler = new SyndicationFeedActionHandler(
            service.Object,
            Mock.Of<ILogger<SyndicationFeedActionHandler>>());
        var context = CreateContext(protocol);

        await handler.OnArtifactUploaded(context, TestContext.Current.CancellationToken);

        service.Verify(value => value.SyndicateArtifactAsync(
                context,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnArtifactUploaded_WhenSyndicationFails_DoesNotFailSourceUpload()
    {
        var service = new Mock<ISyndicationService>();
        service.Setup(value => value.SyndicateArtifactAsync(
                It.IsAny<FeedArtifactEventContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("target unavailable"));
        var handler = new SyndicationFeedActionHandler(
            service.Object,
            Mock.Of<ILogger<SyndicationFeedActionHandler>>());

        await handler.OnArtifactUploaded(
            CreateContext(FeedProtocol.Oci),
            TestContext.Current.CancellationToken);
    }

    private static FeedArtifactEventContext CreateContext(FeedProtocol protocol)
    {
        var surface = new SurfaceContext(
            "default",
            protocol,
            protocol.ToString().ToLowerInvariant(),
            protocol == FeedProtocol.Oci ? "docker" : null,
            "/feed",
            new Uri("https://feed.example.test/"));
        return new FeedArtifactEventContext(surface, "sample/artifact", "1.0.0", null);
    }
}

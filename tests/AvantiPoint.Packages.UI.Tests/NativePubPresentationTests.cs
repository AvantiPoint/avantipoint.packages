using System.Net;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativePubPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PubBrowseUsesPublishedDescriptionAndConstraintsAndDetailDocumentOrder(bool managed)
    {
        await using var openFeed = managed ? null : new NativeUiFactory();
        await using var host = managed ? new ManagedHostFactory() : null;
        using var client = managed ? host!.CreateClient() : openFeed!.CreateClient();
        var services = managed ? host!.Services : openFeed!.Services;
        await NativeUiTestArtifacts.SeedAsync(services, "Pub", "example_sdk", "1.0.0", "2.0.0");
        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IContext>();
            var feed = scope.ServiceProvider.GetRequiredService<IFeedRegistry>().Feed.FeedId;
            var row = await context.NativeArtifacts.SingleAsync(a => a.FeedId == feed && a.Protocol == "Pub" && a.Version == "2.0.0");
            row.MetadataJson = "{\"description\":\"Published description from pubspec\",\"environment\":{\"sdk\":\">=3.0.0 <4.0.0\"}}";
            await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        }
        var browse = WebUtility.HtmlDecode(await client.GetStringAsync("/native/pub"));
        Assert.Contains("Published description from pubspec", browse);
        Assert.Contains("Declared Dart SDK", browse);
        Assert.Contains(">=3.0.0 <4.0.0", browse);
        var detail = await client.GetStringAsync("/native/pub/packages/example_sdk");
        Assert.Contains("Published description from pubspec", detail);
        Assert.True(detail.IndexOf("Published description from pubspec", StringComparison.Ordinal)
            < detail.IndexOf("ADD TO YOUR PROJECT", StringComparison.Ordinal));
    }
}

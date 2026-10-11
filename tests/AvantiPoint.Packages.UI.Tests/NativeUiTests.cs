using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeUiTests
{
    [Theory]
    [InlineData("maven", "Maven")]
    [InlineData("swift", "Swift")]
    [InlineData("pub", "pub")]
    public async Task EnabledFeedsHaveBrowseAndConnectPages(string protocol, string label)
    {
        await using var factory = new NativeUiFactory();
        using var client = factory.CreateClient();
        using var browse = await client.GetAsync($"/native/{protocol}", Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, browse.StatusCode);
        Assert.Contains(label, await browse.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken));
        var connect = await client.GetStringAsync($"/native/{protocol}/feed", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains($"https://registry.test/prefix/{protocol}/", connect);
        Assert.Contains("Authentication", connect);
    }

    [Theory]
    [InlineData("/native/maven")]
    [InlineData("/native/swift/feed")]
    [InlineData("/native/pub/packages/example_sdk")]
    public async Task DisabledFeedsReturn404WithoutResolvingNativeStorage(string path)
    {
        await using var factory = new NativeUiFactory(enabled: false);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path, Xunit.TestContext.Current.CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("/native/maven")]
    [InlineData("/native/swift/packages/example")]
    [InlineData("/native/pub/packages/example_sdk")]
    public async Task PrivateNativeMetadataRequiresAuthorizationOnRazorRoutes(string path)
    {
        await using var factory = new NativeUiFactory(anonymous: false);
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path, Xunit.TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task MavenBrowseRequiresCanonicalPomAndProtocolRoutesCoexistWithUi()
    {
        await using var factory = new NativeUiFactory();
        using var client = factory.CreateClient();
        await NativeUiTestArtifacts.SeedAsync(factory.Services, "Maven", "com.example:sdk", "1.0.0", "9.0.0");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AvantiPoint.Packages.Core.IContext>();
            var feed = scope.ServiceProvider.GetRequiredService<AvantiPoint.Feed.Platform.IFeedRegistry>().Feed.FeedId;
            var artifact = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
                context.NativeArtifacts.Where(artifact => artifact.FeedId == feed && artifact.Protocol == "Maven" && artifact.Version == "9.0.0"),
                Xunit.TestContext.Current.CancellationToken);
            artifact.Path = "com/example/sdk/9.0.0/sdk-9.0.0-sources.pom";
            artifact.PathHash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(artifact.Path)));
            await context.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        }
        var browse = await client.GetStringAsync("/native/maven", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("1.0.0", browse);
        Assert.DoesNotContain("9.0.0", browse);
        var detail = await client.GetStringAsync("/native/maven/packages/com.example%3Asdk", Xunit.TestContext.Current.CancellationToken);
        Assert.DoesNotContain("9.0.0", detail);
        var metadata = await client.GetStringAsync("/maven/com/example/sdk/maven-metadata.xml", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains("<version>1.0.0</version>", metadata);
        Assert.DoesNotContain("9.0.0", metadata);
    }
    [Theory]
    [InlineData("pub", "Pub", "sdk", "1.0.0+2", "1.0.0+10")]
    [InlineData("maven", "Maven", "com.example:sdk", "1.0.0", "1.0.0-sp")]
    [InlineData("swift", "Swift", "sdk", "1.0.0", "2.0.0")]
    public async Task DetailSelectsNativeVersionsAndNeverLeaksNeighboringFeedOrProtocolCase(
        string route, string protocol, string package, string lower, string higher)
    {
        await using var factory = new NativeUiFactory();
        using var client = factory.CreateClient();
        await NativeUiTestArtifacts.SeedAsync(factory.Services, protocol, package, lower, higher);
        var path = $"/native/{route}/packages/{Uri.EscapeDataString(package)}";
        var detail = WebUtility.HtmlDecode(await client.GetStringAsync(path, Xunit.TestContext.Current.CancellationToken));
        Assert.Contains($"Version {higher}", detail);
        Assert.Contains("123 bytes", detail);
        Assert.DoesNotContain("987654", detail);
        Assert.Contains($"https://registry.test/prefix/{route}/", detail);
        var selected = WebUtility.HtmlDecode(await client.GetStringAsync(path + "?version=" + Uri.EscapeDataString(lower), Xunit.TestContext.Current.CancellationToken));
        Assert.Contains($"Version {lower}", selected);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path + "?version=missing", Xunit.TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path.ToUpperInvariant(), Xunit.TestContext.Current.CancellationToken)).StatusCode);
    }

}

using System.Net;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class ManagedHostUiTests
{
    [Fact]
    public async Task HomeRendersWithoutSerializingACallbackAcrossTheInteractiveBoundary()
    {
        await using var factory = new ManagedHostFactory(enabled: false);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/", Xunit.TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("maven")]
    [InlineData("swift")]
    [InlineData("pub")]
    public async Task ManagedHostRendersNativeBrowseAndConnectionInstructions(string protocol)
    {
        await using var factory = new ManagedHostFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/native/{protocol}", Xunit.TestContext.Current.CancellationToken)).StatusCode);
        var connect = await client.GetStringAsync($"/native/{protocol}/feed", Xunit.TestContext.Current.CancellationToken);
        Assert.Contains($"https://registry.test/prefix/{protocol}/", connect);
    }

    [Fact]
    public async Task ManagedHostDetailUsesSharedNativeVersionSelection()
    {
        await using var factory = new ManagedHostFactory();
        using var client = factory.CreateClient();
        await NativeUiTestArtifacts.SeedAsync(factory.Services, "Pub", "sdk", "1.0.0+2", "1.0.0+10");
        var detail = WebUtility.HtmlDecode(await client.GetStringAsync("/native/pub/packages/sdk", Xunit.TestContext.Current.CancellationToken));
        Assert.Contains("Version 1.0.0+10", detail);
        Assert.DoesNotContain("987654", detail);
        var selected = WebUtility.HtmlDecode(await client.GetStringAsync("/native/pub/packages/sdk?version=1.0.0%2B2", Xunit.TestContext.Current.CancellationToken));
        Assert.Contains("Version 1.0.0+2", selected);
    }

    [Theory]
    [InlineData(false, true, HttpStatusCode.NotFound)]
    [InlineData(true, false, HttpStatusCode.Unauthorized)]
    public async Task ManagedHostEnforcesNativeRegistrationAndPrivateMetadataAccess(bool enabled, bool anonymous, HttpStatusCode expected)
    {
        await using var factory = new ManagedHostFactory(enabled, anonymous);
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/native/maven", "/native/swift/feed", "/native/pub/packages/example_sdk" })
            Assert.Equal(expected, (await client.GetAsync(path, Xunit.TestContext.Current.CancellationToken)).StatusCode);
    }
}

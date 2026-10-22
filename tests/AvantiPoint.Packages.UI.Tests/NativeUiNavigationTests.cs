using System.Net;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NativeUiNavigationTests
{
    [Theory]
    [InlineData(false, "maven", "Maven", "com.example:sdk", "1.0.0", "2.0.0")]
    [InlineData(false, "swift", "Swift", "sdk", "1.0.0", "2.0.0")]
    [InlineData(false, "pub", "Pub", "sdk", "1.0.0+2", "1.0.0+10")]
    [InlineData(true, "maven", "Maven", "com.example:sdk", "1.0.0", "2.0.0")]
    [InlineData(true, "swift", "Swift", "sdk", "1.0.0", "2.0.0")]
    [InlineData(true, "pub", "Pub", "sdk", "1.0.0+2", "1.0.0+10")]
    public async Task RepeatedNavigationReselectsVersionsAndRecoversFromMissingVersions(
        bool managed, string route, string protocol, string package, string lower, string higher)
    {
        await using var openFeed = managed ? null : new NativeUiFactory();
        await using var host = managed ? new ManagedHostFactory() : null;
        using var client = managed ? host!.CreateClient() : openFeed!.CreateClient();
        var services = managed ? host!.Services : openFeed!.Services;
        await NativeUiTestArtifacts.SeedAsync(services, protocol, package, lower, higher);
        var detailPath = $"/native/{route}/packages/{Uri.EscapeDataString(package)}";

        foreach (var version in new[] { higher, lower, higher, lower })
        {
            var browse = await client.GetStringAsync($"/native/{route}", Xunit.TestContext.Current.CancellationToken);
            Assert.Contains(Uri.EscapeDataString(package), browse);
            var selected = WebUtility.HtmlDecode(await client.GetStringAsync(
                detailPath + "?version=" + Uri.EscapeDataString(version), Xunit.TestContext.Current.CancellationToken));
            Assert.Contains($"Version {version}", selected);
            Assert.Contains("123 bytes", selected);
            Assert.DoesNotContain("987654", selected);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(
                detailPath + "?version=missing", Xunit.TestContext.Current.CancellationToken)).StatusCode);
            var connect = await client.GetStringAsync($"/native/{route}/feed", Xunit.TestContext.Current.CancellationToken);
            Assert.Contains($"https://registry.test/prefix/{route}/", connect);
        }
    }
}

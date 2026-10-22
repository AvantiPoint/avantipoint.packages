using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class FeedUiPathTests
{
    [Theory]
    [InlineData("/", "/packages/")]
    [InlineData("/npm", "/packages/npm")]
    [InlineData("/oci/docker?tag=latest", "/packages/oci/docker?tag=latest")]
    public void ToHref_PreservesConfiguredApplicationBasePath(string applicationPath, string expected)
    {
        var result = FeedUiPath.ToHref(new Uri("https://packages.example.test/packages/"), applicationPath);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToHref_LeavesExternalLinksUntouched()
    {
        const string external = "https://example.test/package";

        Assert.Equal(external, FeedUiPath.ToHref(new Uri("https://packages.example.test/feed/"), external));
    }
}

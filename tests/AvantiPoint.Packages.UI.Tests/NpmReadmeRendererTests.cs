using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NpmReadmeRendererTests
{
    [Theory]
    [InlineData("[link](javascript:alert('x'))", "javascript:")]
    [InlineData("[link](data:text/html;base64,PHNjcmlwdD4=)", "data:text/html")]
    [InlineData("![image](file:///etc/passwd)", "file:")]
    public void Render_RemovesUnsafeUrlSchemes(string markdown, string unsafeValue)
    {
        var html = NpmReadmeRenderer.Render(markdown);

        Assert.DoesNotContain(unsafeValue, html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_PreservesWebAndRelativeLinks()
    {
        var html = NpmReadmeRenderer.Render(
            "[website](https://example.test/docs) [guide](docs/guide.md) ![logo](./logo.png)");

        Assert.Contains("href=\"https://example.test/docs\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"docs/guide.md\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"./logo.png\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_RewritesRelativeRepositoryLinksAndImages()
    {
        var html = NpmReadmeRenderer.Render(
            "[tests](test.js) [license](/LICENSE) ![logo](./images/logo.png)",
            "git+https://github.com/example/widgets.git",
            "packages/core");

        Assert.Contains(
            "href=\"https://github.com/example/widgets/blob/HEAD/packages/core/test.js\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "href=\"https://github.com/example/widgets/blob/HEAD/LICENSE\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "src=\"https://raw.githubusercontent.com/example/widgets/HEAD/packages/core/images/logo.png\"",
            html,
            StringComparison.Ordinal);
    }
}

namespace AvantiPoint.Packages.UI.Tests;

public sealed class PackageLicenseDisplayTests
{
    [Theory]
    [InlineData("MIT", false, "MIT", "https://spdx.org/licenses/MIT.html")]
    [InlineData("Apache-2.0", false, "Apache-2.0", "https://spdx.org/licenses/Apache-2.0.html")]
    [InlineData("MIT OR Apache-2.0", false, "MIT OR Apache-2.0", "https://licenses.nuget.org/MIT%20OR%20Apache-2.0")]
    [InlineData("MIT", true, "Custom license", null)]
    [InlineData("AvantiPoint-Custom", true, "Custom license", null)]
    [InlineData("AvantiPoint-Custom", false, "Custom license", null)]
    [InlineData("MIT OR Custom", false, "Custom license", null)]
    [InlineData("LICENSE.txt", false, "Custom license", null)]
    [InlineData("MIT OR OR Apache-2.0", false, "Custom license", null)]
    public void DistinguishesStandardExpressionsAndBundledOrCustomLicenses(string expression, bool embedded, string label, string? url)
    {
        Assert.Equal((label, url), PackageLicenseDisplay.Describe(expression, embedded, "https://legacy.example/license"));
    }
}

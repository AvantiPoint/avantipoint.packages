using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class NuGetCommandBuilderTests
{
    [Theory]
    [InlineData("http://localhost:8080/v3/index.json", true)]
    [InlineData("https://packages.example.test/v3/index.json", false)]
    public void InsecureHttpSource_IsExplicitlyOptedIn(string url, bool needsOptIn)
    {
        var command = NuGetCommandBuilder.BuildAddSource(url, "Local feed", null);
        Assert.Equal(needsOptIn, command.Contains("--allow-insecure-connections", StringComparison.Ordinal));
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AuthenticatedSourceCommand_AlwaysIncludesUsernamePlaceholder(string? username)
    {
        var command = NuGetCommandBuilder.BuildAddSource(
            "https://packages.example.test/v3/index.json",
            "AvantiPoint",
            username);

        Assert.Contains("--username \"<email>\"", command, StringComparison.Ordinal);
        Assert.Contains("--password <token>", command, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthenticatedSourceCommand_UsesKnownEmail()
    {
        var command = NuGetCommandBuilder.BuildAddSource(
            "https://packages.example.test/v3/index.json",
            "AvantiPoint",
            "developer@example.test");

        Assert.Contains("--username \"developer@example.test\"", command, StringComparison.Ordinal);
    }
}

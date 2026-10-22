using AvantiPoint.Packages.UI.Services;

namespace AvantiPoint.Packages.UI.Tests;

public class PackageTextDifferTests
{
    [Fact]
    public void Unified_MarksAddedAndRemovedLines()
    {
        var lines = PackageTextDiffer.Unified("alpha\nbeta\n", "alpha\ngamma\n");
        Assert.Contains(lines, static l => l.Kind == "eq" && l.Text == "alpha");
        Assert.Contains(lines, static l => l.Kind == "del" && l.Text == "beta");
        Assert.Contains(lines, static l => l.Kind == "add" && l.Text == "gamma");
    }

    [Fact]
    public void MapSourceLink_ExpandsWildcard()
    {
        var url = PackageSourceText.MapSourceLink(
            @"C:\src\Foo\Bar.cs",
            new Dictionary<string, string> { [@"C:\src\*"] = "https://raw.githubusercontent.com/org/repo/sha/*" });
        Assert.Equal("https://raw.githubusercontent.com/org/repo/sha/Foo/Bar.cs", url);
    }
}

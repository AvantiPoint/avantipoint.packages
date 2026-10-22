using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class DependencyRowTests
{
    [Fact]
    public void LocalDependency_LinksToThisGalleryWithoutAnExternalLookup()
    {
        using var context = new BunitContext();

        var component = context.Render<DependencyRow>(parameters => parameters.Add(
            row => row.Dependency,
            new PackageDependencyInfo
            {
                PackageId = "Example.Dependency",
                VersionRange = "[1.0.0, 2.0.0)",
                IsLocalDependency = true,
            }));

        Assert.Equal("/packages/Example.Dependency", component.Find("a").GetAttribute("href"));
        Assert.DoesNotContain("nuget.org", component.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnavailableDependency_IsNotLinkedToAnUnconfiguredPublicFeed()
    {
        using var context = new BunitContext();

        var component = context.Render<DependencyRow>(parameters => parameters.Add(
            row => row.Dependency,
            new PackageDependencyInfo
            {
                PackageId = "Private.Dependency",
                VersionRange = "1.0.0",
                IsLocalDependency = false,
            }));

        Assert.Empty(component.FindAll("a"));
        Assert.Contains("not currently available on this feed", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nuget.org", component.Markup, StringComparison.OrdinalIgnoreCase);
    }
}

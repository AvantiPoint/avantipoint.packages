using AvantiPoint.Packages.Core;
namespace AvantiPoint.Packages.Search.Tests;

public class PackageOriginFilterTests
{
    [Fact]
    public void ApplyDiscoveryFilter_IncludeMirrored_IncludesPublishedAndMirrored()
    {
        var packages = CreatePackages();
        var options = new SearchOptions { IncludeMirroredPackages = true };

        var result = PackageOriginFilter.ApplyDiscoveryFilter(packages, options).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Origin == PackageOrigin.Published);
        Assert.Contains(result, p => p.Origin == PackageOrigin.Mirrored);
    }

    [Fact]
    public void ApplyDiscoveryFilter_ExcludeMirrored_OnlyPublished()
    {
        var packages = CreatePackages();
        var options = new SearchOptions { IncludeMirroredPackages = false };

        var result = PackageOriginFilter.ApplyDiscoveryFilter(packages, options).ToList();

        Assert.Single(result);
        Assert.Equal(PackageOrigin.Published, result[0].Origin);
    }

  [Fact]
    public void ApplyDiscoveryFilter_NeverIncludesCached()
    {
        var packages = CreatePackages(includeCached: true);
        var options = new SearchOptions { IncludeMirroredPackages = true };

        var result = PackageOriginFilter.ApplyDiscoveryFilter(packages, options).ToList();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, p => p.Origin == PackageOrigin.Cached);
    }

    private static IQueryable<Package> CreatePackages(bool includeCached = false)
    {
        var list = new List<Package>
        {
            new() { Id = "A", Origin = PackageOrigin.Published, Listed = true },
            new() { Id = "B", Origin = PackageOrigin.Mirrored, Listed = true },
        };

        if (includeCached)
        {
            list.Add(new Package { Id = "C", Origin = PackageOrigin.Cached, Listed = true });
        }

        return list.AsQueryable();
    }
}

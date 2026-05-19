using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Search.Tests;

public class SearchVisibilityTests
{
    [Fact]
    public void ComputeMask_PackageWithStableAndPrerelease_IncludesDefaultProfile()
    {
        var packages = new[]
        {
            CreatePackage("1.0.0", isPrerelease: false, semver2: false),
            CreatePackage("2.0.0-beta", isPrerelease: true, semver2: false),
        };

        var mask = SearchVisibility.ComputeMask(packages);

        Assert.True(SearchVisibility.MatchesProfile(mask, includePrerelease: false, includeSemVer2: false));
        Assert.True(SearchVisibility.MatchesProfile(mask, includePrerelease: true, includeSemVer2: false));
    }

    [Fact]
    public void FilterVersions_ExcludesPrereleaseWhenNotRequested()
    {
        var versions = new[] { "1.0.0", "2.0.0-beta" };
        var isPrerelease = new[] { false, true };
        var isSemVer2 = new[] { false, false };

        var filtered = SearchVisibility.FilterVersions(versions, isPrerelease, isSemVer2, includePrerelease: false, includeSemVer2: false);

        Assert.Equal(["1.0.0"], filtered);
    }

    private static Package CreatePackage(string version, bool isPrerelease, bool semver2)
    {
        return new Package
        {
            Id = "Test.Package",
            Listed = true,
            IsPrerelease = isPrerelease,
            SemVerLevel = semver2 ? SemVerLevel.SemVer2 : SemVerLevel.Unknown,
            Version = NuGet.Versioning.NuGetVersion.Parse(version),
            Published = DateTime.UtcNow,
        };
    }
}

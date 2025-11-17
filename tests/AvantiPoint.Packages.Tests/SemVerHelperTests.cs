using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Utilities;
using NuGet.Versioning;
using Xunit;

namespace AvantiPoint.Packages.Tests;

public class SemVerHelperTests
{
    [Theory]
    [InlineData("1.0.0", false)]           // SemVer1
    [InlineData("1.0.0-beta", false)]      // SemVer1 with simple prerelease
    [InlineData("1.0.0-beta.1", true)]     // SemVer2: dot-separated prerelease
    [InlineData("1.0.0-alpha.2.3", true)]  // SemVer2: multiple dots in prerelease
    [InlineData("1.0.0+build123", true)]   // SemVer2: build metadata
    [InlineData("1.0.0-beta+build", true)] // SemVer2: build metadata
    [InlineData("2.1.0", false)]           // SemVer1
    public void IsSemVer2_Version_DetectsCorrectly(string versionString, bool expectedIsSemVer2)
    {
        // Arrange
        var version = NuGetVersion.Parse(versionString);

        // Act
        var result = SemVerHelper.IsSemVer2(version);

        // Assert
        Assert.Equal(expectedIsSemVer2, result);
    }

    [Theory]
    [InlineData("[1.0.0]", false)]              // SemVer1 exact version
    [InlineData("[1.0.0, 2.0.0]", false)]       // SemVer1 range
    [InlineData("[1.0.0-beta.1, 2.0.0]", true)] // SemVer2: min version has dot
    [InlineData("[1.0.0, 2.0.0-rc.1]", true)]   // SemVer2: max version has dot
    [InlineData("[1.0.0+build, )", true)]       // SemVer2: build metadata
    public void IsSemVer2_VersionRange_DetectsCorrectly(string rangeString, bool expectedIsSemVer2)
    {
        // Arrange
        var range = VersionRange.Parse(rangeString);

        // Act
        var result = SemVerHelper.IsSemVer2(range);

        // Assert
        Assert.Equal(expectedIsSemVer2, result);
    }

    [Fact]
    public void IsSemVer2_Package_WithSemVer2Version_ReturnsTrue()
    {
        // Arrange
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0-beta.1"), // SemVer2 version
            Dependencies = new List<PackageDependency>()
        };

        // Act
        var result = SemVerHelper.IsSemVer2(package);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSemVer2_Package_WithSemVer1VersionAndSemVer2Dependency_ReturnsTrue()
    {
        // Arrange
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"), // SemVer1 version
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency
                {
                    Id = "Dependency1",
                    VersionRange = "[1.0.0-beta.1, 2.0.0)" // SemVer2 dependency range
                }
            }
        };

        // Act
        var result = SemVerHelper.IsSemVer2(package);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSemVer2_Package_WithSemVer1VersionAndDependencies_ReturnsFalse()
    {
        // Arrange
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"), // SemVer1 version
            Dependencies = new List<PackageDependency>
            {
                new PackageDependency
                {
                    Id = "Dependency1",
                    VersionRange = "[1.0.0, 2.0.0)" // SemVer1 dependency range
                }
            }
        };

        // Act
        var result = SemVerHelper.IsSemVer2(package);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSemVer2_Package_WithNullDependencies_ReturnsFalseForSemVer1()
    {
        // Arrange
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"), // SemVer1 version
            Dependencies = null
        };

        // Act
        var result = SemVerHelper.IsSemVer2(package);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetSemVerLevel_SemVer2Package_ReturnsSemVer2()
    {
        // Arrange
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0-beta.1"), // SemVer2 version
            Dependencies = new List<PackageDependency>()
        };

        // Act
        var result = SemVerHelper.GetSemVerLevel(package);

        // Assert
        Assert.Equal(SemVerLevel.SemVer2, result);
    }

    [Fact]
    public void GetSemVerLevel_SemVer1Package_ReturnsUnknown()
    {
        // Arrange
        var package = new Package
        {
            Id = "TestPackage",
            Version = NuGetVersion.Parse("1.0.0"), // SemVer1 version
            Dependencies = new List<PackageDependency>()
        };

        // Act
        var result = SemVerHelper.GetSemVerLevel(package);

        // Assert
        Assert.Equal(SemVerLevel.Unknown, result);
    }
}

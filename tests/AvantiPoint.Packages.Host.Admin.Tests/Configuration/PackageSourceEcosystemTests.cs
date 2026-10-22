using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Configuration;

namespace AvantiPoint.Packages.Host.Admin.Tests.Configuration;

public sealed class PackageSourceEcosystemTests
{
    [Theory]
    [InlineData("NuGet", PackageSourceProtocol.NuGet, null)]
    [InlineData("npm", PackageSourceProtocol.Npm, null)]
    [InlineData("Oci", PackageSourceProtocol.Oci, null)]
    [InlineData("Oci:docker", PackageSourceProtocol.Oci, "docker")]
    [InlineData(" oci:helm ", PackageSourceProtocol.Oci, "helm")]
    public void TryParse_MapsHumanEcosystemChoiceToProtocolAndSurface(
        string selection,
        PackageSourceProtocol expectedProtocol,
        string? expectedSurface)
    {
        var parsed = PackageSourceEcosystem.TryParse(selection, out var protocol, out var surface);

        Assert.True(parsed);
        Assert.Equal(expectedProtocol, protocol);
        Assert.Equal(expectedSurface, surface);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Oci:")]
    [InlineData("Maven")]
    public void TryParse_RejectsUnknownOrIncompleteChoices(string? selection)
    {
        Assert.False(PackageSourceEcosystem.TryParse(selection, out _, out _));
    }

    [Theory]
    [InlineData(PackageSourceProtocol.NuGet, null, "NuGet")]
    [InlineData(PackageSourceProtocol.Npm, null, "Npm")]
    [InlineData(PackageSourceProtocol.Oci, null, "Oci")]
    [InlineData(PackageSourceProtocol.Oci, "helm", "Oci:helm")]
    public void FromSource_RoundTripsExistingConfiguration(
        PackageSourceProtocol protocol,
        string? surface,
        string expected)
    {
        var selection = PackageSourceEcosystem.FromSource(new PackageSource
        {
            Protocol = protocol,
            Surface = surface!,
        });

        Assert.Equal(expected, selection);
    }

    [Theory]
    [InlineData("docker", "Docker images")]
    [InlineData("helm", "Helm charts")]
    [InlineData("artifacts", "Artifacts OCI artifacts")]
    public void GetDisplayName_UsesEcosystemLanguage(string surface, string expected)
    {
        Assert.Equal(expected, PackageSourceEcosystem.GetDisplayName(surface));
    }
}

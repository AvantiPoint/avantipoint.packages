using AvantiPoint.Feed.Platform.Configuration;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class OciSurfacePresentationTests
{
    [Theory]
    [InlineData(OciProfile.ContainerImages, "docker", "Docker")]
    [InlineData(OciProfile.HelmCharts, "helm", "Helm")]
    [InlineData(OciProfile.General, null, "OCI")]
    [InlineData(OciProfile.General, "artifacts", "artifacts (OCI)")]
    public void GetDisplayName_UsesEcosystemLanguage(
        OciProfile profile,
        string? segment,
        string expected)
    {
        var options = new OciFeedOptions { Profile = profile };

        Assert.Equal(expected, OciSurfacePresentation.GetDisplayName(options, segment));
    }

    [Fact]
    public void GetDisplayName_UsesConfiguredName()
    {
        var options = new OciFeedOptions
        {
            Profile = OciProfile.ContainerImages,
            DisplayName = "Production images",
        };

        Assert.Equal("Production images", OciSurfacePresentation.GetDisplayName(options, "docker"));
    }

    [Theory]
    [InlineData(OciProfile.ContainerImages, "Docker", "image repositories")]
    [InlineData(OciProfile.HelmCharts, "Helm", "charts")]
    [InlineData(OciProfile.General, "OCI", "artifacts")]
    public void BrowseSemantics_ComeFromProfileRatherThanConfiguredDisplayName(
        OciProfile profile,
        string expectedEcosystem,
        string descriptionPhrase)
    {
        var options = new OciFeedOptions
        {
            Profile = profile,
            DisplayName = "Internal production",
        };

        Assert.Equal(expectedEcosystem, OciSurfacePresentation.GetEcosystemName(options));
        Assert.Contains(
            descriptionPhrase,
            OciSurfacePresentation.GetBrowseDescription(options),
            StringComparison.OrdinalIgnoreCase);
    }
}

using AvantiPoint.Feed.Platform.Configuration;

namespace AvantiPoint.Packages.UI.Services;

public static class OciSurfacePresentation
{
    public static string GetDisplayName(OciFeedOptions options, string? segment)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.DisplayName))
        {
            return options.DisplayName.Trim();
        }

        return options.Profile switch
        {
            OciProfile.ContainerImages => "Docker",
            OciProfile.HelmCharts => "Helm",
            _ when string.IsNullOrWhiteSpace(segment) => "OCI",
            _ => $"{segment!.Trim()} (OCI)",
        };
    }

    public static string GetEcosystemName(OciFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Profile switch
        {
            OciProfile.ContainerImages => "Docker",
            OciProfile.HelmCharts => "Helm",
            _ => "OCI",
        };
    }

    public static string GetBrowseDescription(OciFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Profile switch
        {
            OciProfile.ContainerImages =>
                "Search image repositories, inspect tags, platforms, layers, and immutable digests, then pull with your container client.",
            OciProfile.HelmCharts =>
                "Search charts, compare versions, inspect values and dependencies, then install directly from this registry.",
            _ =>
                "Search artifacts, inspect their manifests and immutable digests, then retrieve them with an OCI client.",
        };
    }
}

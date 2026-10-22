using AvantiPoint.Feed.Platform.Configuration;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Defines which persisted OCI artifacts are discoverable in registry catalogs and the web UI.
/// Cached artifacts remain available for restore-by-reference but are intentionally not discoverable.
/// </summary>
public static class OciDiscoveryPolicy
{
    private static readonly PackageOrigin[] PublishedOnly = [PackageOrigin.Published];
    private static readonly PackageOrigin[] PublishedAndMirrored =
        [PackageOrigin.Published, PackageOrigin.Mirrored];

    public static IReadOnlyList<PackageOrigin> GetVisibleOrigins(OciFeedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.IncludeMirroredInCatalog ? PublishedAndMirrored : PublishedOnly;
    }

    public static bool IsVisible(PackageOrigin origin, OciFeedOptions options) =>
        origin == PackageOrigin.Published
        || (origin == PackageOrigin.Mirrored && options.IncludeMirroredInCatalog);
}

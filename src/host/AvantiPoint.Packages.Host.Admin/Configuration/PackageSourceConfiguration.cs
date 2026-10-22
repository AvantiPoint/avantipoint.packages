using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Host.Admin.Configuration;

/// <summary>
/// Declarative package-source configuration used by the deployable Host.
/// </summary>
/// <remarks>
/// Sources are upserted into the package database during startup. This makes environment
/// variables, mounted configuration files, Helm values, and other ASP.NET Core configuration
/// providers first-class ways to configure upstream registries without requiring a mutable UI
/// setup step.
/// </remarks>
public sealed class PackageSourceConfiguration
{
    public string Name { get; set; } = string.Empty;

    public string FeedUrl { get; set; } = string.Empty;

    public PackageSourceType Type { get; set; } = PackageSourceType.Upstream;

    public PackageSourceProtocol Protocol { get; set; } = PackageSourceProtocol.NuGet;

    public int Priority { get; set; }

    /// <summary>
    /// Optional OCI surface segment, such as <c>docker</c> or <c>helm</c>.
    /// Ignored for NuGet and npm sources.
    /// </summary>
    public string? Surface { get; set; }

    public PackageSourceCachingStrategy CachingStrategy { get; set; } =
        PackageSourceCachingStrategy.IndexAndCache;

    public MirrorRepositorySignaturePolicy MirrorSignaturePolicy { get; set; } =
        MirrorRepositorySignaturePolicy.Resign;

    public bool IsEnabled { get; set; } = true;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? ApiKey { get; set; }
}

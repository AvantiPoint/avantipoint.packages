using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Host.Admin.Configuration;

/// <summary>
/// Translates the protocol-neutral package-source model into the ecosystem choices shown to
/// administrators. Docker and Helm remain OCI sources internally without exposing that routing
/// detail as a second form decision.
/// </summary>
public static class PackageSourceEcosystem
{
    public const string NuGet = "NuGet";
    public const string Npm = "Npm";
    public const string Oci = "Oci";

    public static string ForOciSurface(string surface) =>
        $"{Oci}:{surface.Trim()}";

    public static string FromSource(PackageSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Protocol switch
        {
            PackageSourceProtocol.NuGet => NuGet,
            PackageSourceProtocol.Npm => Npm,
            PackageSourceProtocol.Oci when !string.IsNullOrWhiteSpace(source.Surface) =>
                ForOciSurface(source.Surface),
            PackageSourceProtocol.Oci => Oci,
            _ => throw new ArgumentOutOfRangeException(
                nameof(source),
                source.Protocol,
                "Unknown package source protocol."),
        };
    }

    public static bool TryParse(
        string? value,
        out PackageSourceProtocol protocol,
        out string? surface)
    {
        protocol = PackageSourceProtocol.NuGet;
        surface = null;

        var selection = value?.Trim();
        if (string.Equals(selection, NuGet, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(selection, Npm, StringComparison.OrdinalIgnoreCase))
        {
            protocol = PackageSourceProtocol.Npm;
            return true;
        }

        if (string.Equals(selection, Oci, StringComparison.OrdinalIgnoreCase))
        {
            protocol = PackageSourceProtocol.Oci;
            return true;
        }

        var prefix = $"{Oci}:";
        if (selection?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) != true)
        {
            return false;
        }

        var selectedSurface = selection[prefix.Length..].Trim();
        if (selectedSurface.Length == 0)
        {
            return false;
        }

        protocol = PackageSourceProtocol.Oci;
        surface = selectedSurface;
        return true;
    }

    public static string GetDisplayName(string? surface) => surface?.Trim().ToLowerInvariant() switch
    {
        "docker" => "Docker images",
        "helm" => "Helm charts",
        { Length: > 0 } value => $"{char.ToUpperInvariant(value[0])}{value[1..]} OCI artifacts",
        _ => "OCI artifacts",
    };
}

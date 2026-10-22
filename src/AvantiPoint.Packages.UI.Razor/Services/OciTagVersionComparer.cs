using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Presents OCI references in ecosystem-appropriate package-gallery order.
/// </summary>
public sealed class OciTagVersionComparer : IComparer<string>
{
    /// <summary>
    /// Conventional container-tag ordering: the floating latest tag first, followed by
    /// semantic versions newest-first and other labels alphabetically.
    /// </summary>
    public static OciTagVersionComparer Instance { get; } = new(prioritizeLatest: true);

    /// <summary>
    /// Helm chart ordering: semantic chart versions newest-first, followed by non-version labels.
    /// </summary>
    public static OciTagVersionComparer HelmVersions { get; } = new(prioritizeLatest: false);

    private readonly bool _prioritizeLatest;

    private OciTagVersionComparer(bool prioritizeLatest)
    {
        _prioritizeLatest = prioritizeLatest;
    }

    public int Compare(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }

        if (left is null)
        {
            return 1;
        }

        if (right is null)
        {
            return -1;
        }

        if (_prioritizeLatest)
        {
            var leftIsLatest = left.Equals("latest", StringComparison.OrdinalIgnoreCase);
            var rightIsLatest = right.Equals("latest", StringComparison.OrdinalIgnoreCase);
            if (leftIsLatest != rightIsLatest)
            {
                return leftIsLatest ? -1 : 1;
            }
        }

        var leftIsVersion = TryParseVersion(left, out var leftVersion);
        var rightIsVersion = TryParseVersion(right, out var rightVersion);
        if (leftIsVersion && rightIsVersion)
        {
            var versionComparison = VersionComparer.VersionRelease.Compare(rightVersion, leftVersion);
            return versionComparison != 0
                ? versionComparison
                : StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }

        if (leftIsVersion != rightIsVersion)
        {
            return leftIsVersion ? -1 : 1;
        }

        if (!_prioritizeLatest)
        {
            // A non-version latest alias is still the most useful fallback when a Helm
            // repository has no valid chart-version tag; it just must not outrank SemVer.
            var leftIsLatest = left.Equals("latest", StringComparison.OrdinalIgnoreCase);
            var rightIsLatest = right.Equals("latest", StringComparison.OrdinalIgnoreCase);
            if (leftIsLatest != rightIsLatest)
            {
                return leftIsLatest ? -1 : 1;
            }
        }

        return StringComparer.OrdinalIgnoreCase.Compare(left, right);
    }

    private static bool TryParseVersion(string tag, out NuGetVersion? version)
    {
        var candidate = tag;
        if (candidate.Length > 1
            && (candidate[0] is 'v' or 'V')
            && char.IsAsciiDigit(candidate[1]))
        {
            candidate = candidate[1..];
        }

        // Helm maps SemVer build metadata '+' to '_' when storing a chart as an OCI tag.
        candidate = candidate.Replace('_', '+');
        return NuGetVersion.TryParse(candidate, out version);
    }
}

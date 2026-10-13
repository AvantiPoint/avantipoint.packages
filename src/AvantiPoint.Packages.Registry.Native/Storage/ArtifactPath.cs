using System.Text.RegularExpressions;

namespace AvantiPoint.Packages.Registry.Native.Storage;

public static partial class ArtifactPath
{
    // Reject encoded separators/traversal, absolute paths, hidden paths, platform
    // separators and control characters before any storage access.
    public static bool IsValid(string? path) => path is { Length: > 0 and <= 1024 }
        && path.Split('/').All(IsValidSegment);

    public static bool IsValidSegment(string? segment) => segment is { Length: > 0 and <= 256 }
        && SegmentPattern().IsMatch(segment) && !segment.EndsWith('.')
        && segment != "." && segment != "..";

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_.+\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();
}

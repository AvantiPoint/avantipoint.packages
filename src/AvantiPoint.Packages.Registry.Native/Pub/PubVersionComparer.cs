using System.Numerics;

namespace AvantiPoint.Packages.Registry.Native.Pub;

/// <summary>Pub orders both prerelease and build identifiers numerically/lexically.</summary>
public sealed class PubVersionComparer : IComparer<string>
{
    public static PubVersionComparer Instance { get; } = new();
    public static bool IsPrerelease(string version) => version.Split('+')[0].Contains('-');

    public static string Canonicalize(string version)
    {
        string Components(string text) => string.Join('.', text.Split('.').Select(part => part.All(char.IsAsciiDigit)
            ? BigInteger.Parse(part, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture) : part));
        var build = version.Split('+', 2);
        var main = build[0].Split('-', 2);
        return Components(main[0]) + (main.Length == 1 ? "" : "-" + Components(main[1]))
            + (build.Length == 1 ? "" : "+" + Components(build[1]));
    }

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var leftBuild = x.Split('+', 2);
        var rightBuild = y.Split('+', 2);
        var left = leftBuild[0].Split('-', 2);
        var right = rightBuild[0].Split('-', 2);
        var core = CompareIdentifiers(left[0].Split('.'), right[0].Split('.'));
        if (core != 0) return core;
        if (left.Length != right.Length) return left.Length == 1 ? 1 : -1;
        if (left.Length == 2)
        {
            var prerelease = CompareIdentifiers(left[1].Split('.'), right[1].Split('.'));
            if (prerelease != 0) return prerelease;
        }
        // Pub intentionally differs from SemVer 2 here: a build suffix sorts
        // after no build suffix, and +2 sorts before +10.
        return CompareIdentifiers(leftBuild.Length == 1 ? [] : leftBuild[1].Split('.'),
            rightBuild.Length == 1 ? [] : rightBuild[1].Split('.'));
    }

    private static int CompareIdentifiers(string[] left, string[] right)
    {
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var numericA = left[i].All(char.IsAsciiDigit);
            var numericB = right[i].All(char.IsAsciiDigit);
            var comparison = numericA && numericB
                ? BigInteger.Parse(left[i], System.Globalization.CultureInfo.InvariantCulture)
                    .CompareTo(BigInteger.Parse(right[i], System.Globalization.CultureInfo.InvariantCulture))
                : numericA != numericB ? (numericA ? -1 : 1) : string.CompareOrdinal(left[i], right[i]);
            if (comparison != 0) return comparison;
        }
        return left.Length.CompareTo(right.Length);
    }
}

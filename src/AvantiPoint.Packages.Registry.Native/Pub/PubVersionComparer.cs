using System.Numerics;

namespace AvantiPoint.Packages.Registry.Native.Pub;

/// <summary>SemVer precedence without NuGet's integer size/normalization rules.</summary>
public sealed class PubVersionComparer : IComparer<string>
{
    public static PubVersionComparer Instance { get; } = new();
    public static bool IsPrerelease(string version) => version.Split('+')[0].Contains('-');

    public int Compare(string? x, string? y)
    {
        if (x == y) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var left = x.Split('+')[0].Split('-', 2);
        var right = y.Split('+')[0].Split('-', 2);
        var a = left[0].Split('.');
        var b = right[0].Split('.');
        for (var i = 0; i < 3; i++)
        {
            var comparison = BigInteger.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture)
                .CompareTo(BigInteger.Parse(b[i], System.Globalization.CultureInfo.InvariantCulture));
            if (comparison != 0) return comparison;
        }
        if (left.Length != right.Length) return left.Length == 1 ? 1 : -1;
        if (left.Length == 1) return 0;
        a = left[1].Split('.');
        b = right[1].Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var numericA = a[i].All(char.IsAsciiDigit);
            var numericB = b[i].All(char.IsAsciiDigit);
            var comparison = numericA && numericB
                ? BigInteger.Parse(a[i], System.Globalization.CultureInfo.InvariantCulture)
                    .CompareTo(BigInteger.Parse(b[i], System.Globalization.CultureInfo.InvariantCulture))
                : numericA != numericB ? (numericA ? -1 : 1) : string.CompareOrdinal(a[i], b[i]);
            if (comparison != 0) return comparison;
        }
        return a.Length.CompareTo(b.Length);
    }
}

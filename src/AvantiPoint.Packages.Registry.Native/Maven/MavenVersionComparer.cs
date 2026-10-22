// Adapted from Apache Maven 3.9.11 ComparableVersion (Apache-2.0).
// Copyright The Apache Software Foundation. See THIRD-PARTY-NOTICES.txt.
using System.Numerics;

namespace AvantiPoint.Packages.Registry.Native.Maven;

/// <summary>Maven 3 qualifier/list precedence, rather than NuGet/SemVer precedence.</summary>
public sealed class MavenVersionComparer : IComparer<string>
{
    public static MavenVersionComparer Instance { get; } = new();
    private static readonly string[] Qualifiers = ["alpha", "beta", "milestone", "rc", "snapshot", "", "sp"];

    public int Compare(string? x, string? y) => x == y ? 0 : x is null ? -1 : y is null ? 1 : CompareItems(Parse(x), Parse(y));

    private sealed class Item(BigInteger? number = null, string? qualifier = null, List<Item>? children = null)
    {
        public BigInteger? Number { get; } = number;
        public string? Qualifier { get; } = qualifier;
        public List<Item>? Children { get; } = children;
        public bool IsEmpty => Number?.IsZero ?? (Qualifier is not null ? Qualifier.Length == 0 : Children!.Count == 0);
    }

    private static Item Parse(string version)
    {
        if (version.Length > 128) throw new ArgumentOutOfRangeException(nameof(version));
        var text = version.ToLowerInvariant();
        var root = new Item(children: []);
        var current = root;
        var levels = new Stack<Item>();
        levels.Push(root);
        void Descend()
        {
            var child = new Item(children: []);
            current.Children!.Add(child);
            current = child;
            levels.Push(child);
        }
        var start = 0;
        var digits = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '.' or '-')
            {
                current.Children!.Add(i == start ? new(number: BigInteger.Zero) : Token(text[start..i], digits));
                start = i + 1;
                if (c == '-') Descend();
            }
            else if (char.IsAsciiDigit(c))
            {
                if (!digits && i > start)
                {
                    if (current.Children!.Count != 0) Descend();
                    current.Children!.Add(Token(text[start..i], false, followedByDigit: true));
                    start = i;
                    Descend();
                }
                digits = true;
            }
            else
            {
                if (digits && i > start)
                {
                    current.Children!.Add(Token(text[start..i], true));
                    start = i;
                    Descend();
                }
                digits = false;
            }
        }
        if (text.Length > start)
        {
            if (!digits && current.Children!.Count != 0) Descend();
            current.Children!.Add(Token(text[start..], digits));
        }
        while (levels.TryPop(out var level))
            for (var i = level.Children!.Count - 1; i >= 0; i--)
            {
                var item = level.Children[i];
                if (item.IsEmpty) level.Children.RemoveAt(i);
                else if (item.Children is null) break;
            }
        return root;
    }

    private static Item Token(string text, bool numeric, bool followedByDigit = false)
    {
        if (numeric) return new(number: BigInteger.Parse(text, System.Globalization.CultureInfo.InvariantCulture));
        if (followedByDigit) text = text switch { "a" => "alpha", "b" => "beta", "m" => "milestone", _ => text };
        text = text switch { "ga" or "final" or "release" => "", "cr" => "rc", _ => text };
        return new(qualifier: text);
    }

    private static string QualifierKey(string text)
    {
        var rank = Array.IndexOf(Qualifiers, text);
        return rank < 0 ? "7-" + text : rank.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int CompareItems(Item? left, Item? right)
    {
        if (left is null) return right is null ? 0 : -CompareItems(right, null);
        if (left.Number is { } number)
            return right is null ? number.Sign : right.Number is { } other ? number.CompareTo(other) : 1;
        if (left.Qualifier is { } qualifier)
            return right is null ? string.CompareOrdinal(QualifierKey(qualifier), "5")
                : right.Qualifier is { } other ? string.CompareOrdinal(QualifierKey(qualifier), QualifierKey(other)) : -1;
        if (right is null)
        {
            foreach (var item in left.Children!)
            {
                var comparison = CompareItems(item, null);
                if (comparison != 0) return comparison;
            }
            return 0;
        }
        if (right.Number is not null) return -1;
        if (right.Qualifier is not null) return 1;
        for (var i = 0; i < Math.Max(left.Children!.Count, right.Children!.Count); i++)
        {
            var comparison = CompareItems(i < left.Children.Count ? left.Children[i] : null,
                i < right.Children.Count ? right.Children[i] : null);
            if (comparison != 0) return comparison;
        }
        return 0;
    }
}

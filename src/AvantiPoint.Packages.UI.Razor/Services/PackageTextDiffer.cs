namespace AvantiPoint.Packages.UI.Services;

public static class PackageTextDiffer
{
    public static IReadOnlyList<DiffLine> Unified(string? left, string? right)
    {
        var a = Split(left);
        var b = Split(right);
        if (a.Length == 0 && b.Length == 0)
        {
            return [];
        }

        var n = a.Length;
        var m = b.Length;
        var cap = 8000;
        if (n > cap || m > cap)
        {
            return Truncated(a, b, cap);
        }

        var dp = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                dp[i, j] = string.Equals(a[i], b[j], StringComparison.Ordinal)
                    ? dp[i + 1, j + 1] + 1
                    : Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        var lines = new List<DiffLine>(n + m);
        var li = 0;
        var ri = 0;
        var leftNo = 1;
        var rightNo = 1;
        while (li < n && ri < m)
        {
            if (string.Equals(a[li], b[ri], StringComparison.Ordinal))
            {
                lines.Add(new DiffLine { Kind = "eq", Text = a[li], LeftNumber = leftNo, RightNumber = rightNo });
                li++; ri++; leftNo++; rightNo++;
            }
            else if (dp[li + 1, ri] >= dp[li, ri + 1])
            {
                lines.Add(new DiffLine { Kind = "del", Text = a[li], LeftNumber = leftNo, RightNumber = null });
                li++; leftNo++;
            }
            else
            {
                lines.Add(new DiffLine { Kind = "add", Text = b[ri], LeftNumber = null, RightNumber = rightNo });
                ri++; rightNo++;
            }
        }

        while (li < n)
        {
            lines.Add(new DiffLine { Kind = "del", Text = a[li], LeftNumber = leftNo, RightNumber = null });
            li++; leftNo++;
        }

        while (ri < m)
        {
            lines.Add(new DiffLine { Kind = "add", Text = b[ri], LeftNumber = null, RightNumber = rightNo });
            ri++; rightNo++;
        }

        return lines;
    }

    private static IReadOnlyList<DiffLine> Truncated(string[] a, string[] b, int cap)
    {
        var lines = Unified(
            string.Join('\n', a.Take(cap)),
            string.Join('\n', b.Take(cap))).ToList();
        lines.Add(new DiffLine
        {
            Kind = "eq",
            Text = $"… truncated after {cap} lines …",
            LeftNumber = null,
            RightNumber = null
        });
        return lines;
    }

    private static string[] Split(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}

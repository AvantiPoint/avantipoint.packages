namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Builds a bounded, text-only comparison of the human-facing configuration
/// documents contained in two Helm chart archives. Templates are deliberately
/// excluded: comparing a chart never renders or executes chart content.
/// </summary>
public static class HelmChartComparisonBuilder
{
    private const int MaximumDiffInputCharacters = 256 * 1024;
    private const int MaximumDiffInputLines = 1500;
    private const int MaximumPreviewLines = 800;
    private const int ContextLines = 3;

    public static HelmChartVersionComparison Compare(
        string previousVersion,
        HelmChartDetails previous,
        string currentVersion,
        HelmChartDetails current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var documents = new[]
        {
            CompareDocument("Default values", "values.yaml", previous.ValuesYaml, current.ValuesYaml),
            CompareDocument("Chart metadata", "Chart.yaml", previous.ChartYaml, current.ChartYaml),
            CompareDocument("Values schema", "values.schema.json", previous.ValuesSchemaJson, current.ValuesSchemaJson),
            CompareDocument("Dependency lock", "Chart.lock", previous.ChartLockYaml, current.ChartLockYaml),
        };

        return new HelmChartVersionComparison(
            previousVersion,
            currentVersion,
            documents.Where(static document => document is not null).Select(static document => document!).ToArray());
    }

    private static HelmChartDocumentComparison? CompareDocument(
        string label,
        string fileName,
        string? previousContent,
        string? currentContent)
    {
        if (previousContent is null && currentContent is null)
        {
            return null;
        }

        if (string.Equals(previousContent, currentContent, StringComparison.Ordinal))
        {
            return new HelmChartDocumentComparison(label, fileName, "unchanged", 0, 0, [], false);
        }

        var previousLineCount = CountLines(previousContent);
        var currentLineCount = CountLines(currentContent);
        if ((previousContent?.Length ?? 0) > MaximumDiffInputCharacters
            || (currentContent?.Length ?? 0) > MaximumDiffInputCharacters
            || previousLineCount > MaximumDiffInputLines
            || currentLineCount > MaximumDiffInputLines)
        {
            return new HelmChartDocumentComparison(
                label,
                fileName,
                GetChangeKind(previousContent, currentContent),
                null,
                null,
                [],
                true);
        }

        var lines = PackageTextDiffer.Unified(previousContent, currentContent);
        var added = lines.Count(static line => line.Kind == "add");
        var removed = lines.Count(static line => line.Kind == "del");
        return new HelmChartDocumentComparison(
            label,
            fileName,
            GetChangeKind(previousContent, currentContent),
            added,
            removed,
            Compact(lines),
            false);
    }

    private static string GetChangeKind(string? previousContent, string? currentContent) =>
        previousContent is null ? "added" : currentContent is null ? "removed" : "changed";

    private static int CountLines(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return 0;
        }

        var count = 1;
        foreach (var character in content)
        {
            if (character == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyList<DiffLine> Compact(IReadOnlyList<DiffLine> lines)
    {
        if (lines.Count == 0 || lines.All(static line => line.Kind == "eq"))
        {
            return [];
        }

        var included = new bool[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Kind == "eq")
            {
                continue;
            }

            var start = Math.Max(0, index - ContextLines);
            var end = Math.Min(lines.Count - 1, index + ContextLines);
            for (var contextIndex = start; contextIndex <= end; contextIndex++)
            {
                included[contextIndex] = true;
            }
        }

        var preview = new List<DiffLine>(Math.Min(lines.Count, MaximumPreviewLines));
        var previousIncluded = -2;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!included[index])
            {
                continue;
            }

            if (index > previousIncluded + 1)
            {
                preview.Add(new DiffLine
                {
                    Kind = "skip",
                    Text = "… unchanged lines …",
                    LeftNumber = null,
                    RightNumber = null,
                });
            }

            preview.Add(lines[index]);
            previousIncluded = index;
            if (preview.Count >= MaximumPreviewLines)
            {
                preview.Add(new DiffLine
                {
                    Kind = "skip",
                    Text = "… diff preview truncated …",
                    LeftNumber = null,
                    RightNumber = null,
                });
                break;
            }
        }

        return preview;
    }
}

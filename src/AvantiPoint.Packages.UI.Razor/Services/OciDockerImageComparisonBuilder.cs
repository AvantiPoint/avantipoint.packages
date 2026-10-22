using System.Text.Json;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>
/// Compares bounded OCI manifest and image-configuration projections. Image layers are
/// identified by descriptor only; their content is never downloaded or executed.
/// </summary>
public static class OciDockerImageComparisonBuilder
{
    public static OciDockerImageComparison Compare(
        string previousVersion,
        OciDockerImageDetails previous,
        string currentVersion,
        OciDockerImageDetails current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);

        var previousLayerDigests = previous.Layers
            .Select(static layer => layer.Digest)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currentLayerDigests = current.Layers
            .Select(static layer => layer.Digest)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var previousLayerSequence = previous.Layers.Select(static layer => layer.Digest).ToArray();
        var currentLayerSequence = current.Layers.Select(static layer => layer.Digest).ToArray();
        var sameLayerMembers = previousLayerDigests.SetEquals(currentLayerDigests)
                               && previousLayerSequence.Length == currentLayerSequence.Length;

        return new OciDockerImageComparison(
            previousVersion,
            currentVersion,
            CompareRuntime(previous, current),
            CompareKeyValues(previous.Labels, current.Labels),
            CompareKeyValues(previous.ManifestAnnotations, current.ManifestAnnotations),
            ComparePlatforms(previous.Platforms, current.Platforms),
            CountSharedBaseLayers(previousLayerSequence, currentLayerSequence),
            currentLayerDigests.Count(previousLayerDigests.Contains),
            current.Layers.Where(layer => !previousLayerDigests.Contains(layer.Digest)).ToArray(),
            previous.Layers.Where(layer => !currentLayerDigests.Contains(layer.Digest)).ToArray(),
            sameLayerMembers && !previousLayerSequence.SequenceEqual(
                currentLayerSequence,
                StringComparer.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<OciDockerValueChange> CompareRuntime(
        OciDockerImageDetails previous,
        OciDockerImageDetails current)
    {
        var changes = new List<OciDockerValueChange>();
        AddChange(changes, "Platform", FormatPlatform(previous), FormatPlatform(current));
        AddChange(changes, "Entrypoint", FormatArguments(previous.Entrypoint), FormatArguments(current.Entrypoint));
        AddChange(changes, "Command", FormatArguments(previous.Command), FormatArguments(current.Command));
        AddChange(changes, "Working directory", previous.WorkingDirectory, current.WorkingDirectory);
        AddChange(changes, "User", previous.User, current.User);
        return changes;
    }

    private static IReadOnlyList<OciDockerKeyValueChange> CompareKeyValues(
        IReadOnlyDictionary<string, string>? previous,
        IReadOnlyDictionary<string, string>? current)
    {
        previous ??= new Dictionary<string, string>();
        current ??= new Dictionary<string, string>();
        var keys = previous.Keys
            .Concat(current.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase);
        var changes = new List<OciDockerKeyValueChange>();
        foreach (var key in keys)
        {
            var hadPrevious = previous.TryGetValue(key, out var previousValue);
            var hasCurrent = current.TryGetValue(key, out var currentValue);
            if (hadPrevious && hasCurrent && string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(new OciDockerKeyValueChange(
                key,
                !hadPrevious ? "added" : !hasCurrent ? "removed" : "changed",
                previousValue,
                currentValue));
        }

        return changes;
    }

    private static IReadOnlyList<OciDockerPlatformChange> ComparePlatforms(
        IReadOnlyList<OciDockerPlatform> previous,
        IReadOnlyList<OciDockerPlatform> current)
    {
        var previousByPlatform = ToPlatformMap(previous);
        var currentByPlatform = ToPlatformMap(current);
        var platforms = previousByPlatform.Keys
            .Concat(currentByPlatform.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static platform => platform, StringComparer.OrdinalIgnoreCase);
        var changes = new List<OciDockerPlatformChange>();
        foreach (var platform in platforms)
        {
            var hadPrevious = previousByPlatform.TryGetValue(platform, out var previousValue);
            var hasCurrent = currentByPlatform.TryGetValue(platform, out var currentValue);
            if (hadPrevious
                && hasCurrent
                && string.Equals(previousValue!.Digest, currentValue!.Digest, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            changes.Add(new OciDockerPlatformChange(
                platform,
                !hadPrevious ? "added" : !hasCurrent ? "removed" : "changed",
                previousValue?.Digest,
                currentValue?.Digest));
        }

        return changes;
    }

    private static IReadOnlyDictionary<string, OciDockerPlatform> ToPlatformMap(
        IReadOnlyList<OciDockerPlatform> platforms) => platforms
        .GroupBy(FormatPlatform, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            static group => group.Key,
            static group => group.First(),
            StringComparer.OrdinalIgnoreCase);

    private static int CountSharedBaseLayers(
        IReadOnlyList<string> previous,
        IReadOnlyList<string> current)
    {
        var count = 0;
        while (count < previous.Count
               && count < current.Count
               && previous[count].Equals(current[count], StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }

    private static void AddChange(
        ICollection<OciDockerValueChange> changes,
        string label,
        string? previous,
        string? current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            changes.Add(new OciDockerValueChange(label, previous, current));
        }
    }

    private static string? FormatArguments(IReadOnlyList<string> arguments) =>
        arguments.Count == 0 ? null : JsonSerializer.Serialize(arguments);

    private static string? FormatPlatform(OciDockerImageDetails details) =>
        FormatPlatform(details.OperatingSystem, details.Architecture, details.Variant, nullWhenEmpty: true);

    private static string FormatPlatform(OciDockerPlatform platform) =>
        FormatPlatform(platform.OperatingSystem, platform.Architecture, platform.Variant, nullWhenEmpty: false)!;

    private static string? FormatPlatform(
        string? operatingSystem,
        string? architecture,
        string? variant,
        bool nullWhenEmpty)
    {
        var value = string.Join('/', new[] { operatingSystem, architecture, variant }
            .Where(static item => !string.IsNullOrWhiteSpace(item)));
        return value.Length == 0 && nullWhenEmpty ? null : value.Length == 0 ? "Unknown" : value;
    }
}

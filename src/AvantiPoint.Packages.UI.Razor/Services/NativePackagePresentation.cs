using System.Globalization;
using System.Text.Json;

namespace AvantiPoint.Packages.UI.Services;

public static class NativePackagePresentation
{
    public static string Size(long bytes)
    {
        if (bytes < 1024) return $"{bytes} bytes";
        var value = (double)bytes;
        var units = new[] { "KiB", "MiB", "GiB" };
        var index = -1;
        do { value /= 1024; index++; } while (value >= 1024 && index < units.Length - 1);
        return value.ToString("0.#", CultureInfo.InvariantCulture) + " " + units[index];
    }

    public static string Kind(string path) => path switch
    {
        var value when value.EndsWith(".xcframework.zip", StringComparison.Ordinal) => "XCFramework binary",
        var value when value.EndsWith(".tar.gz", StringComparison.Ordinal) => "Hosted package archive",
        var value when value.EndsWith(".aar", StringComparison.Ordinal) => "Android library",
        var value when value.EndsWith(".jar", StringComparison.Ordinal) => "Java archive",
        var value when value.EndsWith(".pom", StringComparison.Ordinal) => "Maven POM",
        var value when value.EndsWith(".module", StringComparison.Ordinal) => "Gradle metadata",
        var value when value.EndsWith(".asc", StringComparison.Ordinal) => "Detached signature file",
        _ => "Artifact",
    };

    public static string? PubspecValue(string? json, params string[] path)
    {
        if (json is null) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var value = document.RootElement;
            foreach (var part in path)
                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value)) return null;
            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}

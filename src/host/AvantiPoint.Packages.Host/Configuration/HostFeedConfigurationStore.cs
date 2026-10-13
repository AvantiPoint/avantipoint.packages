using System.Text.Json;

namespace AvantiPoint.Packages.Host.Configuration;

public sealed class HostFeedConfigurationStore(IConfiguration configuration, IWebHostEnvironment environment)
{
    public static string ConfigurationPath(IConfiguration configuration, string contentRoot)
    {
        var root = configuration["Storage:Type"] == "FileSystem" ? configuration["Storage:Path"] : null;
        var path = configuration["Host:FeedConfigurationPath"]
            ?? System.IO.Path.Combine(root ?? "App_Data", "feed-settings.json");
        return System.IO.Path.GetFullPath(path, contentRoot);
    }

    public HostFeedConfiguration Active => HostFeedConfiguration.Read(configuration);

    public HostFeedConfiguration ReadSaved()
    {
        using var saved = (ConfigurationRoot)new ConfigurationBuilder().AddConfiguration(configuration)
            .AddJsonFile(ConfigurationPath(configuration, environment.ContentRootPath), optional: true, reloadOnChange: false).Build();
        return HostFeedConfiguration.Read(saved);
    }

    public async Task SaveAsync(HostFeedConfiguration settings, CancellationToken ct)
    {
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(settings, new(settings), errors, validateAllProperties: true))
            throw new ArgumentException("Invalid feed configuration.", nameof(settings));
        object Surface(bool enabled, decimal artifactMiB, decimal expandedMiB, int entries) => new
        {
            Enabled = enabled,
            MaxArtifactBytes = (long)(artifactMiB * 1048576m),
            MaxExpandedArchiveBytes = (long)(expandedMiB * 1048576m),
            MaxArchiveEntries = entries,
        };
        var document = new
        {
            IsReadOnlyMode = settings.ReadOnly,
            Feed = new
            {
                settings.PublicBaseUrl,
                Authentication = new { settings.AllowAnonymousPull },
                Maven = Surface(settings.MavenEnabled, settings.MavenArtifactMiB, settings.MavenExpandedArchiveMiB, settings.MavenMaxArchiveEntries),
                Swift = Surface(settings.SwiftEnabled, settings.SwiftArtifactMiB, settings.SwiftExpandedArchiveMiB, settings.SwiftMaxArchiveEntries),
                Pub = Surface(settings.PubEnabled, settings.PubArtifactMiB, settings.PubExpandedArchiveMiB, settings.PubMaxArchiveEntries),
            },
        };
        var path = ConfigurationPath(configuration, environment.ContentRootPath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), ct);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

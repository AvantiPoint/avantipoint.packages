using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Host.Configuration;

public sealed record HostFeedConfiguration : IValidatableObject
{
    public bool MavenEnabled { get; set; }
    public bool SwiftEnabled { get; set; }
    public bool PubEnabled { get; set; }
    [StringLength(2048)]
    public string? PublicBaseUrl { get; set; }
    public bool AllowAnonymousPull { get; set; }
    public bool ReadOnly { get; set; }
    [Range(typeof(decimal), "0.00000095367431640625", "4096")]
    public decimal MavenArtifactMiB { get; set; } = 256;
    [Range(typeof(decimal), "0.00000095367431640625", "4096")]
    public decimal SwiftArtifactMiB { get; set; } = 256;
    [Range(typeof(decimal), "0.00000095367431640625", "4096")]
    public decimal PubArtifactMiB { get; set; } = 256;
    [Range(typeof(decimal), "0.00000095367431640625", "16384")]
    public decimal SwiftExpandedArchiveMiB { get; set; } = 1024;
    [Range(typeof(decimal), "0.00000095367431640625", "16384")]
    public decimal PubExpandedArchiveMiB { get; set; } = 1024;
    [Range(1, 100000)]
    public int SwiftMaxArchiveEntries { get; set; } = 10000;
    [Range(1, 100000)]
    public int PubMaxArchiveEntries { get; set; } = 10000;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MavenEnabled || SwiftEnabled || PubEnabled || !string.IsNullOrWhiteSpace(PublicBaseUrl))
        {
            if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri)
                || !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback)
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                yield return new("Use an HTTPS public feed URL without credentials, query or fragment. HTTP is allowed only for loopback development.", [nameof(PublicBaseUrl)]);
        }
        foreach (var (expanded, artifact, member) in new[] {
            (SwiftExpandedArchiveMiB, SwiftArtifactMiB, nameof(SwiftExpandedArchiveMiB)),
            (PubExpandedArchiveMiB, PubArtifactMiB, nameof(PubExpandedArchiveMiB)),
        })
            if (expanded < artifact)
                yield return new("Expanded archive limit must be at least this feed's artifact limit.", [member]);
    }

    public static HostFeedConfiguration Read(IConfiguration configuration) => new()
    {
        MavenEnabled = configuration.GetValue<bool>("Feed:Maven:Enabled"),
        SwiftEnabled = configuration.GetValue<bool>("Feed:Swift:Enabled"),
        PubEnabled = configuration.GetValue<bool>("Feed:Pub:Enabled"),
        PublicBaseUrl = configuration["Feed:PublicBaseUrl"],
        AllowAnonymousPull = configuration.GetValue<bool>("Feed:Authentication:AllowAnonymousPull"),
        ReadOnly = configuration.GetValue<bool>("IsReadOnlyMode"),
        MavenArtifactMiB = (configuration.GetValue<long?>("Feed:Maven:MaxArtifactBytes") ?? 256L * 1048576) / 1048576m,
        SwiftArtifactMiB = (configuration.GetValue<long?>("Feed:Swift:MaxArtifactBytes") ?? 256L * 1048576) / 1048576m,
        PubArtifactMiB = (configuration.GetValue<long?>("Feed:Pub:MaxArtifactBytes") ?? 256L * 1048576) / 1048576m,
        SwiftExpandedArchiveMiB = (configuration.GetValue<long?>("Feed:Swift:MaxExpandedArchiveBytes") ?? 1024L * 1048576) / 1048576m,
        PubExpandedArchiveMiB = (configuration.GetValue<long?>("Feed:Pub:MaxExpandedArchiveBytes") ?? 1024L * 1048576) / 1048576m,
        SwiftMaxArchiveEntries = configuration.GetValue<int?>("Feed:Swift:MaxArchiveEntries") ?? 10000,
        PubMaxArchiveEntries = configuration.GetValue<int?>("Feed:Pub:MaxArchiveEntries") ?? 10000,
    };
}

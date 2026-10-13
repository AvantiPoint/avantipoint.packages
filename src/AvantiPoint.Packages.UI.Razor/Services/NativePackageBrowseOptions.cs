namespace AvantiPoint.Packages.UI.Services;

public sealed class NativePackageBrowseOptions
{
    /// <summary>The host's read role. Unconfigured hosts authenticate private reads with feed tokens.</summary>
    public string? AuthenticatedReadRole { get; set; }

    public bool UseBrowserDownloads { get; set; }
}

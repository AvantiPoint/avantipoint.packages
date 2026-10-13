namespace AvantiPoint.Packages.Registry.Native;

public sealed class NativeRegistryOptions
{
    public bool Enabled { get; set; }
    public long MaxArtifactBytes { get; set; } = 256 * 1024 * 1024;
    public long MaxExpandedArchiveBytes { get; set; } = 1024L * 1024 * 1024;
    public int MaxArchiveEntries { get; set; } = 10000;
}

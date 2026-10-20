namespace AvantiPoint.Packages.Registry.Oci;

/// <summary>
/// Controls scheduled OCI garbage collection. Collection is disabled and dry-run-only by default.
/// </summary>
public sealed class OciGarbageCollectionOptions
{
    public bool Enabled { get; set; }

    public bool DryRun { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    public TimeSpan MinimumAge { get; set; } = TimeSpan.FromHours(24);
}

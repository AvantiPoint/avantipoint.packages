using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Gcp;

public class GcsStorageOptions
{
    [Required]
    public string Bucket { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Path to a service account JSON key file. When unset, uses Application Default Credentials.
    /// </summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Host and port for the GCS emulator (e.g. fake-gcs-server). Example: http://localhost:4443
    /// </summary>
    public string? EmulatorHost { get; set; }

    /// <summary>
    /// When true and <see cref="EmulatorHost"/> is set, uses unauthenticated access to the emulator.
    /// </summary>
    public bool UseEmulator { get; set; }
}

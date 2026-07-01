using System;
using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Gcp;

public class GcsStorageOptions : IConnectionStringOptions
{
    /// <summary>
    /// A URI-style connection string, for example
    /// <c>gs://my-bucket?credentialsPath=/keys/sa.json&amp;prefix=packages</c>, or for the emulator
    /// <c>gs://my-bucket?emulatorHost=http://localhost:4443&amp;useEmulator=true</c>.
    /// When supplied, its components populate the fields below.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// The name of a connection string under the root <c>ConnectionStrings</c> section to use for
    /// <see cref="ConnectionString"/> (for example <c>ConnectionStrings__Storage</c>).
    /// </summary>
    public string? ConnectionStringName { get; set; }

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

    /// <summary>
    /// Populates the individual fields from <see cref="ConnectionString"/> when it is a URI-style
    /// value. Fields not present in the connection string are left unchanged. Called as a
    /// post-configure step after any named connection string has been resolved.
    /// </summary>
    public void ApplyConnectionString()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        if (!ConnectionStringUri.TryParse(ConnectionString, out var uri))
        {
            throw new InvalidOperationException(
                "The Google Cloud Storage connection string must be a URI, for example 'gs://bucket?credentialsPath=/keys/sa.json'.");
        }

        if (!string.IsNullOrEmpty(uri.Host)) Bucket = uri.Host;

        if (uri.GetString("credentialsPath") is { Length: > 0 } credentialsPath) CredentialsPath = credentialsPath;
        if (uri.GetString("emulatorHost") is { Length: > 0 } emulatorHost) EmulatorHost = emulatorHost;
        if (uri.GetBool("useEmulator") is { } useEmulator) UseEmulator = useEmulator;

        var prefix = uri.GetString("prefix") ?? uri.Path;
        if (!string.IsNullOrEmpty(prefix)) Prefix = prefix;
    }
}

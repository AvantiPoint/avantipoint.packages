using System;
using System.Collections.Generic;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Denormalized package registration document for external search indexes (one per package id).
/// </summary>
public sealed class PackageSearchDocument
{
    public string Key { get; set; }

    public string Id { get; set; }

    public string Version { get; set; }

    public string Description { get; set; }

    public string[] Authors { get; set; } = [];

    public bool HasEmbeddedIcon { get; set; }

    public string IconUrl { get; set; }

    public string LicenseUrl { get; set; }

    public string ProjectUrl { get; set; }

    public DateTimeOffset Published { get; set; }

    public string Summary { get; set; }

    public string[] Tags { get; set; } = [];

    public string Title { get; set; }

    public long TotalDownloads { get; set; }

    public string[] Versions { get; set; } = [];

    public string[] VersionDownloads { get; set; } = [];

    /// <summary>Parallel to <see cref="Versions"/>; used when filtering version autocomplete.</summary>
    public bool[] VersionIsPrerelease { get; set; } = [];

    /// <summary>Parallel to <see cref="Versions"/>; used when filtering version autocomplete.</summary>
    public bool[] VersionIsSemVer2 { get; set; } = [];

    public string[] Dependencies { get; set; } = [];

    public string[] PackageTypes { get; set; } = [];

    public string[] Frameworks { get; set; } = [];

    /// <summary>Bitmask of search profiles for which this package has at least one matching listed version.</summary>
    public int VisibilityMask { get; set; }

    public PackageOrigin Origin { get; set; }
}

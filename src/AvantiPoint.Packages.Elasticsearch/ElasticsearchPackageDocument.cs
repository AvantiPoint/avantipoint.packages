namespace AvantiPoint.Packages.Elasticsearch;

/// <summary>
/// Indexed document shape for Elasticsearch / OpenSearch.
/// </summary>
public sealed class ElasticsearchPackageDocument
{
    public string Key { get; set; }

    public string Id { get; set; }

    public string Version { get; set; }

    public string Description { get; set; }

    public string[] Authors { get; set; }

    public bool HasEmbeddedIcon { get; set; }

    public string IconUrl { get; set; }

    public string LicenseUrl { get; set; }

    public string ProjectUrl { get; set; }

    public DateTimeOffset Published { get; set; }

    public string Summary { get; set; }

    public string[] Tags { get; set; }

    public string Title { get; set; }

    public long TotalDownloads { get; set; }

    public string[] Versions { get; set; }

    public string[] VersionDownloads { get; set; }

    public string[] Dependencies { get; set; }

    public string[] PackageTypes { get; set; }

    public string[] Frameworks { get; set; }

    public int VisibilityMask { get; set; }

    public bool VisibleForDefaultSearch { get; set; }

    public bool VisibleForSemVer2Search { get; set; }

    public bool VisibleForPrereleaseSearch { get; set; }

    public bool VisibleForFullSearch { get; set; }

    public bool[] VersionIsPrerelease { get; set; }

    public bool[] VersionIsSemVer2 { get; set; }
}

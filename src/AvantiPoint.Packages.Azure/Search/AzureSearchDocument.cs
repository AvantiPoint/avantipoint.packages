using System;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;

namespace AvantiPoint.Packages.Azure.Search;

public sealed class AzureSearchDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    public string Key { get; set; }

    [SearchableField(IsFilterable = true, IsSortable = true)]
    public string Id { get; set; }

    [SearchableField(IsFilterable = true, IsSortable = true)]
    public string Version { get; set; }

    [SearchableField]
    public string Description { get; set; }

    [SearchableField]
    public string[] Authors { get; set; }

    [SimpleField]
    public bool HasEmbeddedIcon { get; set; }

    [SimpleField]
    public string IconUrl { get; set; }

    [SimpleField]
    public string LicenseUrl { get; set; }

    [SimpleField]
    public string ProjectUrl { get; set; }

    [SimpleField(IsSortable = true)]
    public DateTimeOffset Published { get; set; }

    [SearchableField]
    public string Summary { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    public string[] Tags { get; set; }

    [SearchableField]
    public string Title { get; set; }

    [SimpleField(IsSortable = true)]
    public long TotalDownloads { get; set; }

    [SimpleField]
    public string[] Versions { get; set; }

    [SimpleField]
    public string[] VersionDownloads { get; set; }

    [SearchableField(IsFilterable = true)]
    public string[] Dependencies { get; set; }

    [SearchableField(IsFilterable = true)]
    public string[] PackageTypes { get; set; }

    [SearchableField(IsFilterable = true)]
    public string[] Frameworks { get; set; }

    [SimpleField(IsFilterable = true)]
    public int VisibilityMask { get; set; }

    [SimpleField]
    public bool[] VersionIsPrerelease { get; set; }

    [SimpleField]
    public bool[] VersionIsSemVer2 { get; set; }

    [SimpleField(IsFilterable = true)]
    public string Origin { get; set; }
}

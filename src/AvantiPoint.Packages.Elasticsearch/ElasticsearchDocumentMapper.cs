using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Elasticsearch;

internal static class ElasticsearchDocumentMapper
{
    public static ElasticsearchPackageDocument ToElasticsearch(PackageSearchDocument source)
        => new()
        {
            Key = source.Key,
            Id = source.Id,
            Version = source.Version,
            Description = source.Description,
            Authors = source.Authors,
            HasEmbeddedIcon = source.HasEmbeddedIcon,
            IconUrl = source.IconUrl,
            LicenseUrl = source.LicenseUrl,
            ProjectUrl = source.ProjectUrl,
            Published = source.Published,
            Summary = source.Summary,
            Tags = source.Tags,
            Title = source.Title,
            TotalDownloads = source.TotalDownloads,
            Versions = source.Versions,
            VersionDownloads = source.VersionDownloads,
            Dependencies = source.Dependencies,
            PackageTypes = source.PackageTypes,
            Frameworks = source.Frameworks,
            VisibilityMask = source.VisibilityMask,
            VisibleForDefaultSearch = (source.VisibilityMask & 1) != 0,
            VisibleForSemVer2Search = (source.VisibilityMask & 2) != 0,
            VisibleForPrereleaseSearch = (source.VisibilityMask & 4) != 0,
            VisibleForFullSearch = (source.VisibilityMask & 8) != 0,
            VersionIsPrerelease = source.VersionIsPrerelease,
            VersionIsSemVer2 = source.VersionIsSemVer2,
        };

    public static PackageSearchDocument FromElasticsearch(ElasticsearchPackageDocument source)
        => new()
        {
            Key = source.Key,
            Id = source.Id,
            Version = source.Version,
            Description = source.Description,
            Authors = source.Authors ?? [],
            HasEmbeddedIcon = source.HasEmbeddedIcon,
            IconUrl = source.IconUrl,
            LicenseUrl = source.LicenseUrl,
            ProjectUrl = source.ProjectUrl,
            Published = source.Published,
            Summary = source.Summary,
            Tags = source.Tags ?? [],
            Title = source.Title,
            TotalDownloads = source.TotalDownloads,
            Versions = source.Versions ?? [],
            VersionDownloads = source.VersionDownloads ?? [],
            Dependencies = source.Dependencies ?? [],
            PackageTypes = source.PackageTypes ?? [],
            Frameworks = source.Frameworks ?? [],
            VisibilityMask = source.VisibilityMask,
            VersionIsPrerelease = source.VersionIsPrerelease,
            VersionIsSemVer2 = source.VersionIsSemVer2,
        };
}

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
            SearchFilters = source.SearchFilters,
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
            SearchFilters = source.SearchFilters,
        };
}

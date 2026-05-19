using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Azure.Search;

internal static class AzureSearchDocumentMapper
{
    public static AzureSearchDocument ToAzure(PackageSearchDocument source)
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
            VersionIsPrerelease = source.VersionIsPrerelease,
            VersionIsSemVer2 = source.VersionIsSemVer2,
        };

    public static PackageSearchDocument FromAzure(AzureSearchDocument source)
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

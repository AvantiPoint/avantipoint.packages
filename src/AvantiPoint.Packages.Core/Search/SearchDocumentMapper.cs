using System;
using System.Collections.Generic;
using System.Linq;
using AvantiPoint.Packages.Protocol.Models;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core;

public class SearchDocumentMapper
{
    private readonly IUrlGenerator _url;

    public SearchDocumentMapper(IUrlGenerator url)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
    }

    public SearchResponse MapSearch(IReadOnlyList<PackageSearchDocument> documents, long totalHits)
    {
        var results = documents.Select(MapSearchResult).ToList();
        return new SearchResponse
        {
            TotalHits = totalHits,
            Data = results,
            Context = SearchContext.Default(_url.GetPackageMetadataResourceUrl()),
        };
    }

    public SearchResult MapSearchResult(PackageSearchDocument document)
    {
        var versions = new List<SearchResultVersion>();
        if (document.Versions.Length == document.VersionDownloads.Length)
        {
            for (var i = 0; i < document.Versions.Length; i++)
            {
                var version = NuGetVersion.Parse(document.Versions[i]);
                versions.Add(new SearchResultVersion
                {
                    RegistrationLeafUrl = _url.GetRegistrationLeafUrl(document.Id, version),
                    Version = document.Versions[i],
                    Downloads = long.Parse(document.VersionDownloads[i]),
                });
            }
        }

        var latestVersion = NuGetVersion.Parse(document.Version);
        var iconUrl = document.HasEmbeddedIcon
            ? _url.GetPackageIconDownloadUrl(document.Id, latestVersion)
            : document.IconUrl;

        return new SearchResult
        {
            PackageId = document.Id,
            Version = document.Version,
            Description = document.Description,
            Authors = document.Authors,
            IconUrl = iconUrl,
            LicenseUrl = document.LicenseUrl,
            ProjectUrl = document.ProjectUrl,
            RegistrationIndexUrl = _url.GetRegistrationIndexUrl(document.Id),
            Summary = document.Summary,
            Tags = document.Tags,
            Title = document.Title,
            TotalDownloads = document.TotalDownloads,
            PackageTypes = document.PackageTypes.Select(t => new SearchResultPackageType { Name = t }).ToList(),
            Versions = versions,
        };
    }
}

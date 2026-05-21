using System;
using System.Collections.Generic;
using AvantiPoint.Packages.Protocol.Models;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    internal record PackageSearchQueryResult(
        string Id,
        NuGetVersion Version,
        string Description,
        string[] Authors,
        bool HasEmbeddedIcon,
        bool HasEmbeddedLicense,
        string IconUrl,
        string LicenseUrl,
        string ProjectUrl,
        DateTime Published,
        string Summary,
        string[] Tags,
        string Title,
        long TotalDownloads,
        List<SearchResultPackageType> PackageTypes);
}

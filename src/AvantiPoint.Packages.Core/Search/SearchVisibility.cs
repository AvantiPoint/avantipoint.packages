using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core;

/// <summary>
/// Computes which NuGet search filter profiles a package registration supports and filters indexed versions.
/// Profiles match <see cref="DatabaseSearchService"/> row-level filtering (four combinations of prerelease / SemVer2).
/// </summary>
public static class SearchVisibility
{
    public const int ProfileCount = 4;

    public static int GetProfile(bool includePrerelease, bool includeSemVer2)
        => (includePrerelease ? 2 : 0) | (includeSemVer2 ? 1 : 0);

    public static int GetProfileBit(bool includePrerelease, bool includeSemVer2)
        => 1 << GetProfile(includePrerelease, includeSemVer2);

    public static bool VersionMatches(bool isPrerelease, bool isSemVer2, bool includePrerelease, bool includeSemVer2)
        => (includePrerelease || !isPrerelease) && (includeSemVer2 || !isSemVer2);

    public static bool VersionMatches(Package package, bool includePrerelease, bool includeSemVer2)
        => VersionMatches(package.IsPrerelease, package.SemVerLevel == SemVerLevel.SemVer2, includePrerelease, includeSemVer2);

    public static int ComputeMask(IEnumerable<Package> versions)
    {
        var list = versions as IReadOnlyList<Package> ?? versions.ToList();
        var mask = 0;

        for (var profile = 0; profile < ProfileCount; profile++)
        {
            var includePrerelease = (profile & 2) != 0;
            var includeSemVer2 = (profile & 1) != 0;

            if (list.Any(v => VersionMatches(v, includePrerelease, includeSemVer2)))
            {
                mask |= 1 << profile;
            }
        }

        return mask;
    }

    public static bool MatchesProfile(int visibilityMask, bool includePrerelease, bool includeSemVer2)
        => (visibilityMask & GetProfileBit(includePrerelease, includeSemVer2)) != 0;

    public static IReadOnlyList<string> FilterVersions(
        string[] versions,
        bool[] versionIsPrerelease,
        bool[] versionIsSemVer2,
        bool includePrerelease,
        bool includeSemVer2)
    {
        if (versions.Length == 0)
        {
            return versions;
        }

        if (versionIsPrerelease.Length != versions.Length || versionIsSemVer2.Length != versions.Length)
        {
            return versions;
        }

        var result = new List<string>(versions.Length);
        for (var i = 0; i < versions.Length; i++)
        {
            if (VersionMatches(versionIsPrerelease[i], versionIsSemVer2[i], includePrerelease, includeSemVer2))
            {
                result.Add(versions[i]);
            }
        }

        return result;
    }

    public static PackageSearchDocument FilterForRequest(
        PackageSearchDocument document,
        bool includePrerelease,
        bool includeSemVer2)
    {
        if (!MatchesProfile(document.VisibilityMask, includePrerelease, includeSemVer2))
        {
            return null;
        }

        var filteredVersions = FilterVersions(
            document.Versions,
            document.VersionIsPrerelease,
            document.VersionIsSemVer2,
            includePrerelease,
            includeSemVer2);

        if (filteredVersions.Count == 0)
        {
            return null;
        }

        var latestVersion = filteredVersions
            .Select(NuGetVersion.Parse)
            .OrderByDescending(v => v)
            .First()
            .ToFullString();

        var filteredDownloads = FilterVersionDownloads(document, filteredVersions);

        return new PackageSearchDocument
        {
            Key = document.Key,
            Id = document.Id,
            Version = latestVersion,
            Description = document.Description,
            Authors = document.Authors,
            HasEmbeddedIcon = document.HasEmbeddedIcon,
            IconUrl = document.IconUrl,
            LicenseUrl = document.LicenseUrl,
            ProjectUrl = document.ProjectUrl,
            Published = document.Published,
            Summary = document.Summary,
            Tags = document.Tags,
            Title = document.Title,
            TotalDownloads = document.TotalDownloads,
            Versions = filteredVersions.ToArray(),
            VersionDownloads = filteredDownloads,
            VersionIsPrerelease = FilterFlags(document.VersionIsPrerelease, document.Versions, filteredVersions),
            VersionIsSemVer2 = FilterFlags(document.VersionIsSemVer2, document.Versions, filteredVersions),
            Dependencies = document.Dependencies,
            PackageTypes = document.PackageTypes,
            Frameworks = document.Frameworks,
            VisibilityMask = document.VisibilityMask,
            Origin = document.Origin,
        };
    }

    private static string[] FilterVersionDownloads(PackageSearchDocument document, IReadOnlyList<string> filteredVersions)
    {
        if (document.Versions.Length != document.VersionDownloads.Length)
        {
            return [];
        }

        var downloads = new string[filteredVersions.Count];
        for (var i = 0; i < filteredVersions.Count; i++)
        {
            var index = Array.IndexOf(document.Versions, filteredVersions[i]);
            downloads[i] = index >= 0 ? document.VersionDownloads[index] : "0";
        }

        return downloads;
    }

    private static bool[] FilterFlags(bool[] flags, string[] allVersions, IReadOnlyList<string> filteredVersions)
    {
        if (flags.Length != allVersions.Length)
        {
            return [];
        }

        var result = new bool[filteredVersions.Count];
        for (var i = 0; i < filteredVersions.Count; i++)
        {
            var index = Array.IndexOf(allVersions, filteredVersions[i]);
            result[i] = index >= 0 && flags[index];
        }

        return result;
    }
}

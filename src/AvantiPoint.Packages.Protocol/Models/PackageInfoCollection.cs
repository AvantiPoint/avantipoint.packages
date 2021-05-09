using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Protocol.Models
{
    public class PackageInfoCollection
    {
        private NuGetVersion _version;
        private PackageInfo _latest => _version is null ?
            Versions.FirstOrDefault(x => x.IsListed) :
            Versions.FirstOrDefault(x => x.Version.OriginalVersion == _version.OriginalVersion && x.IsListed);

        public PackageInfoCollection()
        {
        }

        public PackageInfoCollection(string version)
        {
            _version = NuGetVersion.Parse(version);
        }

        [JsonPropertyName("packageId")]
        public string PackageId => _latest?.PackageId;

        [JsonPropertyName("authors")]
        public IEnumerable<string> Authors => _latest?.Authors ?? Array.Empty<string>();

        [JsonPropertyName("description")]
        public string Description => _latest.Description;

        [JsonPropertyName("totalDownloads")]
        public long TotalDownloads => Versions.Select(x => x.Downloads).Sum();

        [JsonPropertyName("downloadsPerDay")]
        public long DownloadsPerDay
        {
            get
            {
                var firstPublished = Versions.OrderBy(x => x.Published).Select(x => x.Published).First();
                var timeSinceFirstPublish = DateTimeOffset.Now - firstPublished;
                var days = (double)(timeSinceFirstPublish.TotalDays < 1 ? 1 : (int)timeSinceFirstPublish.TotalDays);
                return (long)((double)TotalDownloads / days);
            }
        }

        [JsonPropertyName("hasReadme")]
        public bool HasReadme => _latest?.HasReadme ?? false;

        [JsonPropertyName("isListed")]
        public bool IsListed => _latest?.IsListed ?? false;

        [JsonPropertyName("isDeprecated")]
        public bool IsDeprecated => _latest?.IsDeprecated ?? false;

        [JsonPropertyName("published")]
        public DateTimeOffset Published => _latest?.Published ?? DateTimeOffset.Now;

        [JsonPropertyName("summary")]
        public string Summary => _latest?.Summary;

        [JsonPropertyName("iconUrl")]
        public string IconUrl => _latest?.IconUrl;

        [JsonPropertyName("licenseUrl")]
        public string LicenseUrl => _latest?.LicenseUrl;

        [JsonPropertyName("projectUrl")]
        public string ProjectUrl => _latest?.ProjectUrl;

        [JsonPropertyName("repositoryUrl")]
        public string RepositoryUrl => _latest?.RepositoryUrl;

        [JsonPropertyName("repositoryType")]
        public string RepositoryType => _latest?.RepositoryType;

        [JsonPropertyName("tags")]
        public IEnumerable<string> Tags => _latest?.Tags ?? Array.Empty<string>();

        [JsonPropertyName("version")]
        public NuGetVersion Version => _latest?.Version;

        [JsonPropertyName("isPrerelease")]
        public bool IsPrerelease => _latest?.IsPrerelease ?? false;

        [JsonPropertyName("isTool")]
        public bool IsTool => _latest?.IsTool ?? false;

        [JsonPropertyName("isDevelopmentDependency")]
        public bool IsDevelopmentDependency => _latest?.IsDevelopmentDependency ?? false;

        [JsonPropertyName("isTemplate")]
        public bool IsTemplate => _latest?.IsTemplate ?? false;

        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes => _latest?.ReleaseNotes;

        [JsonPropertyName("dependencies")]
        public Dictionary<string, IEnumerable<PackageDependencyInfo>> Dependencies => _latest?.Dependencies ?? new Dictionary<string, IEnumerable<PackageDependencyInfo>>();

        private List<PackageInfo> _versions;
        [JsonPropertyName("versions")]
        public List<PackageInfo> Versions
        {
            get => _versions?.OrderByDescending(x => x.Version)?.ThenByDescending(x => x.Published)?.ToList() ?? new List<PackageInfo>();
            set => _versions = value;
        }

        public void SetSelectedVersion(NuGetVersion version)
        {
            _version = version;
        }
    }
}

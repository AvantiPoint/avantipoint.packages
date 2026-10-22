using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Core
{
    public class APPackagesServiceIndex : IServiceIndexService
    {
        private readonly IUrlGenerator _url;
        private readonly PackageFeedOptions _options;
        private readonly IVulnerabilityService _vulnerabilityService;

        public APPackagesServiceIndex(
            IUrlGenerator url,
            IOptions<PackageFeedOptions> options,
            IVulnerabilityService vulnerabilityService)
        {
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _vulnerabilityService = vulnerabilityService
                ?? throw new ArgumentNullException(nameof(vulnerabilityService));
        }

        private IEnumerable<ServiceIndexItem> BuildResource(string name, string url, params string[] versions)
        {
            foreach (var version in versions)
            {
                var type = string.IsNullOrEmpty(version) ? name : $"{name}/{version}";

                yield return new ServiceIndexItem
                {
                    ResourceUrl = url,
                    Type = type,
                };
            }
        }

        public async Task<ServiceIndexResponse> GetAsync(CancellationToken cancellationToken = default)
        {
            var resources = new List<ServiceIndexItem>();

            resources.AddRange(BuildResource("PackagePublish", _url.GetPackagePublishResourceUrl(), "2.0.0"));
            resources.AddRange(BuildResource("SymbolPackagePublish", _url.GetSymbolPublishResourceUrl(), "4.9.0"));
            resources.AddRange(BuildResource("SearchQueryService", _url.GetSearchResourceUrl(), "", "3.0.0-beta", "3.0.0-rc", "3.5.0"));

            // Registration hives - SemVer1 only, identity encoding (legacy)
            resources.AddRange(BuildResource("RegistrationsBaseUrl", _url.GetPackageMetadataResourceUrl(), "", "3.0.0-rc", "3.0.0-beta"));

            // Registration hives - SemVer1 only, gzipped (3.4.0)
            resources.AddRange(BuildResource("RegistrationsBaseUrl", _url.GetPackageMetadataResourceGzipSemVer1Url(), "3.4.0"));

            // Registration hives - SemVer2 capable, gzipped (3.6.0 and Versioned)
            resources.AddRange(BuildResource("RegistrationsBaseUrl", _url.GetPackageMetadataResourceGzipSemVer2Url(), "3.6.0"));
            resources.Add(new ServiceIndexItem
            {
                ResourceUrl = _url.GetPackageMetadataResourceGzipSemVer2Url(),
                Type = "RegistrationsBaseUrl/Versioned",
                ClientVersion = "4.3.0-alpha"
            });

            resources.AddRange(BuildResource("PackageBaseAddress", _url.GetPackageContentResourceUrl(), "3.0.0"));
            resources.AddRange(BuildResource("SearchAutocompleteService", _url.GetAutocompleteResourceUrl(), "", "3.0.0-rc", "3.0.0-beta", "3.5.0"));
            resources.AddRange(BuildResource("ReadmeUriTemplate", _url.GetPackageReadmeResourceUrl(), "6.13.0"));

            // NuGet requires VulnerabilityInfo to be omitted when there are no pages.
            // Advertising an empty or malformed index causes NU1900 during restore.
            if (_options.EnableVulnerabilityInfo
                && await _vulnerabilityService.GetLatestUpdateTimestampAsync(cancellationToken) is not null)
            {
                resources.AddRange(BuildResource("VulnerabilityInfo", _url.GetVulnerabilityIndexUrl(), "6.7.0"));
            }

            // Expose RepositorySignatures endpoint for certificate information
            resources.AddRange(BuildResource("RepositorySignatures", _url.GetRepositorySignaturesUrl(), "5.0.0"));

            var result = new ServiceIndexResponse
            {
                Version = "3.0.0",
                Resources = resources,
            };

            return result;
        }
    }
}

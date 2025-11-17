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

        public APPackagesServiceIndex(IUrlGenerator url, IOptions<PackageFeedOptions> options)
        {
            _url = url ?? throw new ArgumentNullException(nameof(url));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
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

        public Task<ServiceIndexResponse> GetAsync(CancellationToken cancellationToken = default)
        {
            var resources = new List<ServiceIndexItem>();

            resources.AddRange(BuildResource("PackagePublish", _url.GetPackagePublishResourceUrl(), "2.0.0"));
            resources.AddRange(BuildResource("SymbolPackagePublish", _url.GetSymbolPublishResourceUrl(), "4.9.0"));
            resources.AddRange(BuildResource("SearchQueryService", _url.GetSearchResourceUrl(), "", "3.0.0-beta", "3.0.0-rc", "3.5.0"));
            resources.AddRange(BuildResource("RegistrationsBaseUrl", _url.GetPackageMetadataResourceUrl(), "", "3.0.0-rc", "3.0.0-beta"));
            resources.AddRange(BuildResource("PackageBaseAddress", _url.GetPackageContentResourceUrl(), "3.0.0"));
            resources.AddRange(BuildResource("SearchAutocompleteService", _url.GetAutocompleteResourceUrl(), "", "3.0.0-rc", "3.0.0-beta", "3.5.0"));
            resources.AddRange(BuildResource("ReadmeUriTemplate", _url.GetPackageReadmeResourceUrl(), "6.13.0"));
            
            // Always expose VulnerabilityInfo to avoid NuGet client CLI warnings
            // The endpoint will return empty results if the feature is disabled
            resources.AddRange(BuildResource("VulnerabilityInfo", _url.GetVulnerabilityIndexUrl(), "6.7.0"));

            var result = new ServiceIndexResponse
            {
                Version = "3.0.0",
                Resources = resources,
            };

            return Task.FromResult(result);
        }
    }
}

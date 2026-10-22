using System;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Hosting
{
    // TODO: This should validate the "Host" header against known valid values
    public class NuGetFeedUrlGenerator : IUrlGenerator
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LinkGenerator _linkGenerator;
        private readonly IPublicBaseUrlProvider _publicBaseUrlProvider;
        private readonly IFeedRegistry _feedRegistry;

        public NuGetFeedUrlGenerator(
            IHttpContextAccessor httpContextAccessor,
            LinkGenerator linkGenerator,
            IPublicBaseUrlProvider publicBaseUrlProvider,
            IFeedRegistry feedRegistry)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));
            _publicBaseUrlProvider = publicBaseUrlProvider ?? throw new ArgumentNullException(nameof(publicBaseUrlProvider));
            _feedRegistry = feedRegistry ?? throw new ArgumentNullException(nameof(feedRegistry));
        }

        public string GetServiceIndexUrl()
        {
            return RouteUrl(Routes.IndexRouteName) ?? AbsoluteUrl("v3/index.json");
        }

        public string GetPackageContentResourceUrl()
        {
            return AbsoluteUrl("v3/package");
        }

        public string GetPackageMetadataResourceUrl()
        {
            return AbsoluteUrl("v3/registration");
        }

        public string GetPackageMetadataResourceGzipSemVer1Url()
        {
            return AbsoluteUrl("v3/registration-gz-semver1");
        }

        public string GetPackageMetadataResourceGzipSemVer2Url()
        {
            return AbsoluteUrl("v3/registration-gz-semver2");
        }

        public string GetPackagePublishResourceUrl()
        {
            return RouteUrl(Routes.UploadPackageRouteName);
        }

        public string GetSymbolPublishResourceUrl()
        {
            return RouteUrl(Routes.UploadSymbolRouteName);
        }

        public string GetSearchResourceUrl()
        {
            return RouteUrl(Routes.SearchRouteName);
        }

        public string GetAutocompleteResourceUrl()
        {
            return RouteUrl(Routes.AutocompleteRouteName);
        }

        public string GetRegistrationIndexUrl(string id)
        {
            return RouteUrl(
                Routes.RegistrationIndexRouteName,
                new { Id = id.ToLowerInvariant() });
        }

        public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper)
        {
            // AvantiPoint Packages does not support paging the registration resource.
            throw new NotImplementedException();
        }

        public string GetRegistrationLeafUrl(string id, NuGetVersion version)
        {
            return RouteUrl(
                Routes.RegistrationLeafRouteName,
                new
                {
                    Id = id.ToLowerInvariant(),
                    Version = version.ToNormalizedString().ToLowerInvariant(),
                });
        }

        public string GetPackageVersionsUrl(string id)
        {
            return RouteUrl(
                Routes.PackageVersionsRouteName,
                new { Id = id.ToLowerInvariant() });
        }

        public string GetPackageDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return RouteUrl(
                Routes.PackageDownloadRouteName,
                new
                {
                    Id = id,
                    Version = versionString,
                    IdVersion = $"{id}.{versionString}"
                });
        }

        public string GetPackageManifestDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return RouteUrl(
                Routes.PackageDownloadRouteName,
                new
                {
                    Id = id,
                    Version = versionString,
                    Id2 = id,
                });
        }

        public string GetPackageIconDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return RouteUrl(
                Routes.PackageDownloadIconRouteName,
                new
                {
                    Id = id,
                    Version = versionString
                });
        }

        public string GetPackageLicenseDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return RouteUrl(
                Routes.PackageDownloadLicenseRouteName,
                new
                {
                    Id = id,
                    Version = versionString
                });
        }

        public string GetVulnerabilityIndexUrl()
        {
            return RouteUrl(Routes.VulnerabilityIndexRouteName);
        }

        public string GetPackageReadmeResourceUrl()
        {
            // Return the template URL with placeholders for use in the service index
            return AbsoluteUrl("v3/package/{lower_id}/{lower_version}/readme");
        }

        public string GetPackageReadmeDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return RouteUrl(
                Routes.PackageDownloadReadmeRouteName,
                new
                {
                    Id = id,
                    Version = versionString
                });
        }

        public string GetRepositorySignaturesUrl()
        {
            return RouteUrl(Routes.RepositorySignaturesRouteName);
        }

        public string GetCertificateDownloadUrl(string fingerprint)
        {
            return RouteUrl(Routes.CertificateDownloadRouteName, new { fingerprint });
        }

        private string RouteUrl(string routeName, object values = null)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("A request context is required to generate feed URLs.");
            var path = _linkGenerator.GetPathByRouteValues(httpContext, routeName, values);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var requestPathBase = httpContext.Request.PathBase.Value;
            if (!string.IsNullOrEmpty(requestPathBase)
                && path.StartsWith(requestPathBase, StringComparison.OrdinalIgnoreCase))
            {
                path = path[requestPathBase.Length..];
            }

            return AbsoluteUrl(path);
        }

        private string AbsoluteUrl(string relativePath)
        {
            var httpContext = _httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException("A request context is required to generate feed URLs.");
            var routePrefix = _feedRegistry.TryGetNuGetSurface()?.RoutePrefix ?? string.Empty;
            var publicBase = _publicBaseUrlProvider
                .GetSurfacePublicBaseUrl(httpContext, routePrefix)
                .ToString()
                .TrimEnd('/');

            var path = relativePath.TrimStart('/');
            var normalizedPrefix = routePrefix.Trim('/');
            if (!string.IsNullOrEmpty(normalizedPrefix)
                && path.StartsWith(normalizedPrefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                path = path[(normalizedPrefix.Length + 1)..];
            }

            return $"{publicBase}/{path}";
        }
    }
}

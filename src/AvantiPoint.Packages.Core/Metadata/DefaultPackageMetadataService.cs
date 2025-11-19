using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.Protocol.Utilities;
using Microsoft.EntityFrameworkCore;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using PackageDependencyInfo = AvantiPoint.Packages.Protocol.Models.PackageDependencyInfo;

namespace AvantiPoint.Packages.Core
{
    /// <inheritdoc />
    public class DefaultPackageMetadataService : IPackageMetadataService
    {
        private readonly IMirrorService _mirror;
        private readonly IPackageService _packages;
        private readonly RegistrationBuilder _builder;
        private readonly IContext _context;
        private readonly IUrlGenerator _urlGenerator;

        public DefaultPackageMetadataService(
            IContext context,
            IMirrorService mirror,
            IPackageService packages,
            RegistrationBuilder builder,
            IUrlGenerator urlGenerator)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mirror = mirror ?? throw new ArgumentNullException(nameof(mirror));
            _packages = packages ?? throw new ArgumentNullException(nameof(packages));
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _urlGenerator = urlGenerator;
        }

        public async Task<NuGetApiRegistrationIndexResponse> GetRegistrationIndexOrNullAsync(
            string packageId,
            CancellationToken cancellationToken = default)
        {
            // Default behavior includes all packages (SemVer1 + SemVer2)
            return await GetRegistrationIndexOrNullAsync(packageId, includeSemVer2: true, cancellationToken);
        }

        public async Task<NuGetApiRegistrationIndexResponse> GetRegistrationIndexOrNullAsync(
            string packageId,
            bool includeSemVer2,
            CancellationToken cancellationToken = default)
        {
            var packages = await FindPackagesOrNullAsync(packageId, cancellationToken);
            if (packages == null)
            {
                return null;
            }

            // Filter packages based on SemVer level
            var filteredPackages = FilterPackagesBySemVer(packages, includeSemVer2);
            if (!filteredPackages.Any())
            {
                return null;
            }

            return _builder.BuildIndex(
                new PackageRegistration(
                    packageId,
                    filteredPackages));
        }

        public async Task<RegistrationLeafResponse> GetRegistrationLeafOrNullAsync(
            string id,
            NuGetVersion version,
            CancellationToken cancellationToken = default)
        {
            // Default behavior includes all packages (SemVer1 + SemVer2)
            return await GetRegistrationLeafOrNullAsync(id, version, includeSemVer2: true, cancellationToken);
        }

        public async Task<RegistrationLeafResponse> GetRegistrationLeafOrNullAsync(
            string id,
            NuGetVersion version,
            bool includeSemVer2,
            CancellationToken cancellationToken = default)
        {
            // Allow read-through caching to happen if it is configured.
            await _mirror.MirrorAsync(id, version, cancellationToken);

            var package = await _packages.FindOrNullAsync(id, version, includeUnlisted: true, cancellationToken);
            if (package == null)
            {
                return null;
            }

            // Filter by SemVer level - if this is a SemVer2 package and includeSemVer2 is false, return null
            if (!includeSemVer2 && Utilities.SemVerHelper.IsSemVer2(package))
            {
                return null;
            }

            return _builder.BuildLeaf(package);
        }

        private async Task<IReadOnlyList<Package>> FindPackagesOrNullAsync(
            string packageId,
            CancellationToken cancellationToken)
        {
            var upstreamPackages = await _mirror.FindPackagesOrNullAsync(packageId, cancellationToken);
            var localPackages = await _packages.FindAsync(packageId, includeUnlisted: true, cancellationToken);

            if (upstreamPackages == null)
            {
                return localPackages.Any()
                    ? localPackages
                    : null;
            }

            // Mrge the local packages into the upstream packages.
            var result = upstreamPackages.ToDictionary(p => new PackageIdentity(p.Id, p.Version));
            var local = localPackages.ToDictionary(p => new PackageIdentity(p.Id, p.Version));

            foreach (var localPackage in local)
            {
                result[localPackage.Key] = localPackage.Value;
            }

            return result.Values.ToList();
        }

        public async Task<PackageInfoCollection> GetPackageInfo(
            string packageId,
            string version = default,
            CancellationToken cancellationToken = default)
        {
            var query = packageId.ToLower();
            var result = await _context.Packages
                .Include(x => x.Dependencies)
                .Include(x => x.PackageTypes)
                .Where(x => x.Id.ToLower() == packageId.ToLower())
                .ToListAsync();

            if (!result.Any())
                return new PackageInfoCollection();

            // Get all unique dependency package IDs
            var allDependencyIds = result
                .SelectMany(x => x.Dependencies.Select(d => d.Id))
                .Distinct()
                .ToList();

            // Check which dependencies exist locally in a single query
            var localDependencies = await _context.Packages
                .Where(p => allDependencyIds.Contains(p.Id))
                .Select(p => p.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            var localDependencySet = new HashSet<string>(localDependencies, StringComparer.OrdinalIgnoreCase);

            // Get download counts for all versions in one query
            var downloadCounts = await _context.PackageDownloads
                .Where(pd => result.Select(p => p.Key).Contains(pd.PackageKey))
                .GroupBy(pd => pd.PackageKey)
                .Select(g => new { PackageKey = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var downloadDict = downloadCounts.ToDictionary(x => x.PackageKey, x => x.Count);

            var packageInfo = string.IsNullOrEmpty(version) ? new PackageInfoCollection() : new PackageInfoCollection(version);
            packageInfo.Versions = result
                .Select(x => new PackageInfo
                {
                    PackageId = x.Id,
                    Authors = x.Authors,
                    Dependencies = x.Dependencies
                        .GroupBy(x => x.TargetFramework)
                        .ToDictionary(x => x.Key, x => x.Select(d => new PackageDependencyInfo
                        {
                            PackageId = d.Id,
                            VersionRange = $"({VersionHelper.GetFormattedVersionConstraint(d.VersionRange)})",
                            IsLocalDependency = localDependencySet.Contains(d.Id)
                        })),
                    Description = x.Description,
#pragma warning disable CS0618
                    Downloads = (downloadDict.ContainsKey(x.Key) ? downloadDict[x.Key] : 0) + x.Downloads,
#pragma warning restore CS0618
                    HasReadme = x.HasReadme,
                    IsListed = x.Listed,
                    IsPrerelease = x.IsPrerelease,
                    IsDevelopmentDependency = x.IsDevelopmentDependency,
                    IsTool = x.IsTool,
                    IsTemplate = x.PackageTypes.Any(x => x.Name.Equals("Template", StringComparison.OrdinalIgnoreCase)),
                    IsDeprecated = false, // TODO: BaGet will need to add support for deprecation
                    IconUrl = x.HasEmbeddedIcon ? _urlGenerator.GetPackageIconDownloadUrl(x.Id, x.Version) : x.IconUrlString,
                    LicenseUrl = x.LicenseUrlString,
                    LicenseExpression = x.LicenseExpression,
                    HasEmbeddedLicense = x.HasEmbeddedLicense,
                    ProjectUrl = x.ProjectUrlString,
                    Published = x.Published,
                    ReleaseNotes = x.ReleaseNotes,
                    RequireLicenseAcceptance = x.RequireLicenseAcceptance,
                    RepositoryType = x.RepositoryType,
                    RepositoryUrl = x.RepositoryUrlString,
                    Summary = x.Summary,
                    Tags = x.Tags,
                    Version = x.Version
                })
                .OrderByDescending(x => x.Version)
                .ToList();
            return packageInfo;
        }

        private IReadOnlyList<Package> FilterPackagesBySemVer(IReadOnlyList<Package> packages, bool includeSemVer2)
        {
            if (includeSemVer2)
            {
                // Include all packages
                return packages;
            }

            // Filter out SemVer2 packages
            return packages.Where(p => !Utilities.SemVerHelper.IsSemVer2(p)).ToList();
        }
    }
}

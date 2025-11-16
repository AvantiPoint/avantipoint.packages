using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Core
{
    public class PackageService : IPackageService
    {
        private readonly IContext _context;
        private readonly IHttpContextAccessor _contextAccessor;

        public PackageService(IContext context, IHttpContextAccessor contextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
        }

        public async Task<PackageAddResult> AddAsync(Package package, CancellationToken cancellationToken)
        {
            try
            {
                _context.Packages.Add(package);

                await _context.SaveChangesAsync(cancellationToken);

                return PackageAddResult.Success;
            }
            catch (DbUpdateException e)
                when (_context.IsUniqueConstraintViolationException(e))
            {
                return PackageAddResult.PackageAlreadyExists;
            }
        }

        public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken)
        {
            return await _context
                .Packages
                .Where(p => p.Id == id)
                .AnyAsync(cancellationToken);
        }

        public async Task<bool> ExistsAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return await _context
                .Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .AnyAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Package>> FindAsync(string id, bool includeUnlisted, CancellationToken cancellationToken)
        {
            // Optimized: Use view with JSON columns if available (relational databases),
            // fallback to traditional includes for non-relational providers (e.g., Cosmos DB)
            if (_context.PackagesWithJsonData != null)
            {
                // Use the optimized view - single query with JSON aggregation done by database
                var viewQuery = _context.PackagesWithJsonData
                    .AsNoTracking()
                    .Where(p => p.Id == id);

                if (!includeUnlisted)
                {
                    viewQuery = viewQuery.Where(p => p.Listed);
                }

                var viewPackages = await viewQuery.ToListAsync(cancellationToken);
                
                // Convert view entities to Package entities with deserialized relationships
                return viewPackages.Select(ConvertFromView).ToList().AsReadOnly();
            }
            else
            {
                // Fallback for non-relational providers (e.g., Cosmos DB, Azure Table Storage)
                var query = _context.Packages
                    .AsNoTracking()
                    .Include(p => p.Dependencies)
                    .Include(p => p.PackageTypes)
                    .Include(p => p.TargetFrameworks)
                    .Where(p => p.Id == id);

                if (!includeUnlisted)
                {
                    query = query.Where(p => p.Listed);
                }

                var packages = await query.ToListAsync(cancellationToken);
                return packages.AsReadOnly();
            }
        }

        private static Package ConvertFromView(PackageWithJsonData view)
        {
            var package = new Package
            {
                Key = view.Key,
                Id = view.Id,
                NormalizedVersionString = view.NormalizedVersionString,
                OriginalVersionString = view.OriginalVersionString,
                Authors = view.Authors ?? [],
                Description = view.Description ?? string.Empty,
                Downloads = view.Downloads,
                HasReadme = view.HasReadme,
                HasEmbeddedIcon = view.HasEmbeddedIcon,
                HasEmbeddedLicense = view.HasEmbeddedLicense,
                IsPrerelease = view.IsPrerelease,
                ReleaseNotes = view.ReleaseNotes,
                Language = view.Language,
                Listed = view.Listed,
                LicenseExpression = view.LicenseExpression,
                IsSigned = view.IsSigned,
                IsTool = view.IsTool,
                IsDevelopmentDependency = view.IsDevelopmentDependency,
                MinClientVersion = view.MinClientVersion,
                Published = view.Published,
                RequireLicenseAcceptance = view.RequireLicenseAcceptance,
                SemVerLevel = view.SemVerLevel,
                Summary = view.Summary,
                Title = view.Title,
                IconUrl = view.IconUrl,
                LicenseUrl = view.LicenseUrl,
                ProjectUrl = view.ProjectUrl,
                RepositoryUrl = view.RepositoryUrl,
                RepositoryType = view.RepositoryType,
                RepositoryCommit = view.RepositoryCommit,
                RepositoryCommitDate = view.RepositoryCommitDate,
                Tags = view.Tags ?? [],
                IsDeprecated = view.IsDeprecated,
                DeprecationReasons = view.DeprecationReasons ?? [],
                DeprecationMessage = view.DeprecationMessage,
                DeprecatedAlternatePackageId = view.DeprecatedAlternatePackageId,
                DeprecatedAlternatePackageVersionRange = view.DeprecatedAlternatePackageVersionRange,
                RowVersion = view.RowVersion ?? [],
                
                // Deserialize JSON columns to entity lists
                Dependencies = DeserializeDependencies(view.DependenciesJson),
                PackageTypes = DeserializePackageTypes(view.PackageTypesJson),
                TargetFrameworks = DeserializeTargetFrameworks(view.TargetFrameworksJson)
            };

            return package;
        }

        private static List<PackageDependency> DeserializeDependencies(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                var items = JsonSerializer.Deserialize<List<JsonDependency>>(json);
                return items?.Select(d => new PackageDependency
                {
                    Id = d.Id,
                    VersionRange = d.VersionRange,
                    TargetFramework = d.TargetFramework
                }).ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static List<PackageType> DeserializePackageTypes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                var items = JsonSerializer.Deserialize<List<JsonPackageType>>(json);
                return items?.Select(pt => new PackageType
                {
                    Name = pt.Name,
                    Version = pt.Version
                }).ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }

        private static List<TargetFramework> DeserializeTargetFrameworks(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];

            try
            {
                var items = JsonSerializer.Deserialize<List<JsonTargetFramework>>(json);
                return items?.Select(tf => new TargetFramework
                {
                    Moniker = tf.Moniker
                }).ToList() ?? [];
            }
            catch
            {
                return [];
            }
        }

        // Helper classes for JSON deserialization
        private class JsonDependency
        {
            public string? Id { get; set; }
            public string? VersionRange { get; set; }
            public string? TargetFramework { get; set; }
        }

        private class JsonPackageType
        {
            public string? Name { get; set; }
            public string? Version { get; set; }
        }

        private class JsonTargetFramework
        {
            public string? Moniker { get; set; }
        }

        public async Task<IReadOnlyList<NuGetVersion>> FindVersionsAsync(string id, bool includeUnlisted, CancellationToken cancellationToken)
        {
            var query = _context.Packages
                .AsNoTracking()
                .Where(p => p.Id == id);

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            var versions = await query.Select(x => x.Version).ToListAsync(cancellationToken);
            return versions.AsReadOnly();
        }

        public Task<Package> FindOrNullAsync(
            string id,
            NuGetVersion version,
            bool includeUnlisted,
            CancellationToken cancellationToken)
        {
            var query = _context.Packages
                .Include(p => p.Dependencies)
                .Include(p => p.TargetFrameworks)
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString());

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            return query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        }

        public Task<bool> UnlistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return TryUpdatePackageAsync(id, version, p => p.Listed = false, cancellationToken);
        }

        public Task<bool> RelistPackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            return TryUpdatePackageAsync(id, version, p => p.Listed = true, cancellationToken);
        }

        public async Task<bool> AddDownloadAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .FirstOrDefaultAsync();

            if(package == null)
            {
                return false;
            }

            var request = _contextAccessor.HttpContext.Request;
            var user = _contextAccessor.HttpContext.User;
            var userName = user.Identity.IsAuthenticated ? user.Identity.Name : "anonymous";
            string client = null;
            string clientVersion = null;
            string clientPlatform = null;
            string clientPlatformVersion = null;
            if(request.Headers.TryGetValue("User-Agent", out var userAgent) && !string.IsNullOrEmpty(userAgent))
            {
                var info = AgentParser.Parse(userAgent);
                client = info.Name;
                clientVersion = info.Version;
                clientPlatform = info.Platform;
                clientPlatformVersion = info.PlatformVersion;
            }

            _context.PackageDownloads.Add(new PackageDownload
            {
                PackageKey = package.Key,
                RemoteIp = _contextAccessor.HttpContext.Connection.RemoteIpAddress,
                ClientPlatform = clientPlatform,
                ClientPlatformVersion = clientPlatformVersion,
                NuGetClient = client,
                NuGetClientVersion = clientVersion,
                User = userName,
                UserAgentString = userAgent,
            });

            return await _context.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> HardDeletePackageAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .Include(p => p.Dependencies)
                .Include(p => p.TargetFrameworks)
                .FirstOrDefaultAsync(cancellationToken);

            if (package == null)
            {
                return false;
            }

            _context.Packages.Remove(package);
            await _context.SaveChangesAsync(cancellationToken);

            return true;
        }

        private async Task<bool> TryUpdatePackageAsync(
            string id,
            NuGetVersion version,
            Action<Package> action,
            CancellationToken cancellationToken)
        {
            var package = await _context.Packages
                .Where(p => p.Id == id)
                .Where(p => p.NormalizedVersionString == version.ToNormalizedString())
                .FirstOrDefaultAsync();

            if (package != null)
            {
                action(package);
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }

            return false;
        }
    }
}

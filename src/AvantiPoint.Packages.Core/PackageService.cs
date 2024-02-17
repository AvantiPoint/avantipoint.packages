using System;
using System.Collections.Generic;
using System.Linq;
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
            // TODO: Refactor this... this is a very expensive query...
            var query = _context.Packages
                .AsNoTracking()
                .Include(p => p.Dependencies)
                .Include(p => p.PackageTypes)
                .Include(p => p.TargetFrameworks)
                .Include(p => p.PackageDownloads)
                .Where(p => p.Id == id);

            if (!includeUnlisted)
            {
                query = query.Where(p => p.Listed);
            }

            var packages = await query.ToListAsync(cancellationToken);
            return packages.AsReadOnly();
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

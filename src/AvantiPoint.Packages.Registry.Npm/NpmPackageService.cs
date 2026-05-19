using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Registry.Npm;

public sealed class NpmPackageService : INpmPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IContext _context;
    private readonly IPathBlobStore _blobStore;
    private readonly ILogger<NpmPackageService> _logger;

    public NpmPackageService(
        IContext context,
        IStorageBackendFactory storageFactory,
        ILogger<NpmPackageService> logger)
    {
        _context = context;
        _blobStore = storageFactory.CreatePathStore("npm/");
        _logger = logger;
    }

    public async Task<JsonObject?> GetPackumentAsync(
        string feedId,
        string packageName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizePackageName(packageName);
        var package = await _context.NpmPackages
            .AsNoTracking()
            .Include(p => p.Versions)
            .Include(p => p.DistTags)
            .FirstOrDefaultAsync(
                p => p.FeedId == feedId && p.Name == normalizedName,
                cancellationToken);

        if (package is null)
        {
            return null;
        }

        return BuildPackument(package);
    }

    public async Task<Stream?> GetTarballAsync(
        string feedId,
        string packageName,
        string tarballFileName,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizePackageName(packageName);
        var package = await _context.NpmPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FeedId == feedId && p.Name == normalizedName, cancellationToken);

        if (package is null)
        {
            return null;
        }

        var version = await _context.NpmVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.FeedId == feedId
                     && v.PackageKey == package.Key
                     && v.TarballPath.EndsWith(tarballFileName),
                cancellationToken);

        if (version is null)
        {
            return null;
        }

        return await _blobStore.GetAsync(version.TarballPath, cancellationToken);
    }

    public async Task PublishAsync(
        string feedId,
        string packageName,
        string version,
        Stream tarball,
        JsonObject versionMetadata,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizePackageName(packageName);
        var tarballFileName = GetTarballFileName(normalizedName, version);

        using var sha1 = SHA1.Create();
        await using var hashStream = new MemoryStream();
        await tarball.CopyToAsync(hashStream, cancellationToken);
        hashStream.Position = 0;
        var shasum = Convert.ToHexString(sha1.ComputeHash(hashStream)).ToLowerInvariant();
        hashStream.Position = 0;

        var storagePath = $"{EncodePackagePath(normalizedName)}/-/{tarballFileName}";
        await _blobStore.PutAsync(storagePath, hashStream, cancellationToken);

        var package = await _context.NpmPackages
            .Include(p => p.Versions)
            .Include(p => p.DistTags)
            .FirstOrDefaultAsync(
                p => p.FeedId == feedId && p.Name == normalizedName,
                cancellationToken);

        if (package is null)
        {
            package = new NpmPackage
            {
                FeedId = feedId,
                Name = normalizedName,
                CreatedAt = DateTime.UtcNow,
            };
            _context.NpmPackages.Add(package);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var versionEntity = package.Versions.FirstOrDefault(v => v.Version == version);
        var versionJson = versionMetadata.DeepClone().AsObject();
        versionJson["version"] = version;
        versionJson["name"] = normalizedName;

        var dist = versionJson["dist"] as JsonObject ?? new JsonObject();
        dist["shasum"] = shasum;
        dist["tarball"] = tarballFileName;
        versionJson["dist"] = dist;

        if (versionEntity is null)
        {
            versionEntity = new NpmVersion
            {
                FeedId = feedId,
                PackageKey = package.Key,
                Version = version,
                TarballPath = storagePath,
                Shasum = shasum,
                PackumentJson = versionJson.ToJsonString(JsonOptions),
                Origin = PackageOrigin.Published,
                Published = DateTime.UtcNow,
            };
            _context.NpmVersions.Add(versionEntity);
            package.Versions.Add(versionEntity);
        }
        else
        {
            versionEntity.TarballPath = storagePath;
            versionEntity.Shasum = shasum;
            versionEntity.PackumentJson = versionJson.ToJsonString(JsonOptions);
        }

        await UpsertDistTagAsync(package, "latest", version, feedId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published npm package {Package}@{Version} to feed {FeedId}",
            normalizedName,
            version,
            feedId);
    }

    private async Task UpsertDistTagAsync(
        NpmPackage package,
        string tag,
        string version,
        string feedId,
        CancellationToken cancellationToken)
    {
        var existing = package.DistTags.FirstOrDefault(t => t.Tag == tag);
        if (existing is null)
        {
            existing = new NpmDistTag
            {
                FeedId = feedId,
                PackageKey = package.Key,
                Tag = tag,
                Version = version,
            };
            _context.NpmDistTags.Add(existing);
            package.DistTags.Add(existing);
        }
        else
        {
            existing.Version = version;
        }

        await Task.CompletedTask;
    }

    private JsonObject BuildPackument(NpmPackage package)
    {
        var packument = new JsonObject
        {
            ["name"] = package.Name,
            ["dist-tags"] = new JsonObject(),
            ["versions"] = new JsonObject(),
        };

        var distTags = (JsonObject)packument["dist-tags"]!;
        foreach (var tag in package.DistTags)
        {
            distTags[tag.Tag] = tag.Version;
        }

        var versions = (JsonObject)packument["versions"]!;
        foreach (var version in package.Versions)
        {
            versions[version.Version] = JsonNode.Parse(version.PackumentJson)!.AsObject();
        }

        return packument;
    }

    internal static string NormalizePackageName(string packageName) =>
        Uri.UnescapeDataString(packageName);

    internal static string EncodePackagePath(string packageName) =>
        packageName.Replace("/", "%2f", StringComparison.Ordinal);

    internal static string GetTarballFileName(string packageName, string version)
    {
        var shortName = packageName.Contains('@')
            ? packageName[(packageName.IndexOf('/') + 1)..]
            : packageName;
        return $"{shortName}-{version}.tgz";
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AvantiPoint.Feed.Platform.Configuration;

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
    private readonly INpmMirrorService _mirror;
    private readonly IMirrorPolicyService _policy;
    private readonly NpmFeedOptions _npmOptions;
    private readonly ILogger<NpmPackageService> _logger;

    public NpmPackageService(
        IContext context,
        IStorageBackendFactory storageFactory,
        INpmMirrorService mirror,
        IMirrorPolicyService policy,
        IOptions<NpmFeedOptions> npmOptions,
        ILogger<NpmPackageService> logger)
    {
        _context = context;
        _blobStore = storageFactory.CreatePathStore("npm/");
        _mirror = mirror;
        _policy = policy;
        _npmOptions = npmOptions.Value;
        _logger = logger;
    }

    public async Task<JsonObject?> GetPackumentAsync(
        string feedId,
        string packageName,
        Uri publicBaseUrl,
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
            var mirrored = await TryMirrorPackumentAsync(feedId, normalizedName, publicBaseUrl, cancellationToken);
            return mirrored;
        }

        return BuildPackument(package, publicBaseUrl);
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

        var expectedTarballPath = $"{EncodePackagePath(normalizedName)}/-/{tarballFileName}";

        if (package is null)
        {
            await TryMirrorPackumentAsync(
                feedId,
                normalizedName,
                new Uri("http://localhost/npm/"),
                cancellationToken);

            package = await _context.NpmPackages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.FeedId == feedId && p.Name == normalizedName, cancellationToken);

            if (package is null)
            {
                return null;
            }
        }

        var version = await _context.NpmVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.FeedId == feedId
                     && v.PackageKey == package.Key
                     && v.TarballPath == expectedTarballPath,
                cancellationToken);

        if (version is null)
        {
            return null;
        }

        var stream = await _blobStore.GetAsync(version.TarballPath, cancellationToken);
        if (stream is not null)
        {
            return stream;
        }

        if (_mirror.Strategy == MirrorCachingStrategy.ProxyOnly)
        {
            var versionJson = JsonNode.Parse(version.PackumentJson)?.AsObject();
            var tarballUrl = versionJson?["dist"]?["tarball"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(tarballUrl))
            {
                return await _mirror.FetchTarballAsync(tarballUrl, cancellationToken);
            }
        }

        return null;
    }

    public async Task<NpmSearchResult> SearchAsync(
        string feedId,
        string? query,
        int from,
        int size,
        CancellationToken cancellationToken = default)
    {
        var packages = _context.NpmPackages.AsNoTracking().Where(p => p.FeedId == feedId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            packages = packages.Where(p => EF.Functions.Like(p.Name, $"%{term}%"));
        }

        var versions = _context.NpmVersions.AsNoTracking().Where(v => v.FeedId == feedId);
        var joined = packages
            .Select(p => new
            {
                p.Name,
                Latest = p.Versions
                    .OrderByDescending(v => v.Published)
                    .Select(v => new { v.Version, v.Published, v.Origin, v.PackumentJson })
                    .FirstOrDefault(),
            })
            .Where(x => x.Latest != null);

        var rows = await joined.ToListAsync(cancellationToken);
        var visible = rows
            .Where(r => _policy.IncludeInDiscovery(FeedProtocol.Npm, r.Latest!.Origin))
            .Skip(from)
            .Take(size)
            .ToList();

        var objects = visible.Select(r =>
        {
            var meta = JsonNode.Parse(r.Latest!.PackumentJson)?.AsObject();
            return new NpmSearchObject(
                r.Name,
                r.Latest.Version,
                meta?["description"]?.GetValue<string>(),
                r.Latest.Published);
        }).ToList();

        var total = rows.Count(r => _policy.IncludeInDiscovery(FeedProtocol.Npm, r.Latest!.Origin));
        return new NpmSearchResult(total, objects);
    }

    private async Task<JsonObject?> TryMirrorPackumentAsync(
        string feedId,
        string normalizedName,
        Uri publicBaseUrl,
        CancellationToken cancellationToken)
    {
        if (_mirror.Strategy == MirrorCachingStrategy.ProxyOnly)
        {
            return await _mirror.FetchPackumentAsync(normalizedName, cancellationToken);
        }

        var upstream = await _mirror.FetchPackumentAsync(normalizedName, cancellationToken);
        if (upstream is null)
        {
            return null;
        }

        var versions = upstream["versions"] as JsonObject;
        if (versions is null)
        {
            return null;
        }

        foreach (var entry in versions)
        {
            if (entry.Value is not JsonObject versionJson)
            {
                continue;
            }

            var version = entry.Key;
            var tarballUrl = versionJson["dist"]?["tarball"]?.GetValue<string>();
            if (string.IsNullOrEmpty(tarballUrl))
            {
                continue;
            }

            await using var tarball = await _mirror.FetchTarballAsync(tarballUrl, cancellationToken);
            if (tarball is null)
            {
                continue;
            }

            versionJson["version"] = version;
            versionJson["name"] = normalizedName;
            await PublishMirroredAsync(feedId, normalizedName, version, tarball, versionJson, publicBaseUrl, cancellationToken);
        }

        return await GetPackumentAsync(feedId, normalizedName, publicBaseUrl, cancellationToken);
    }

    private async Task PublishMirroredAsync(
        string feedId,
        string packageName,
        string version,
        Stream tarball,
        JsonObject versionMetadata,
        Uri publicBaseUrl,
        CancellationToken cancellationToken)
    {
        await PublishAsync(feedId, packageName, version, tarball, versionMetadata, publicBaseUrl, cancellationToken);

        var normalizedName = NormalizePackageName(packageName);
        var versionEntity = await _context.NpmVersions
            .FirstOrDefaultAsync(
                v => v.FeedId == feedId
                     && v.Version == version
                     && v.TarballPath.Contains(GetTarballFileName(normalizedName, version), StringComparison.Ordinal),
                cancellationToken);

        if (versionEntity is not null)
        {
            versionEntity.Origin = _mirror.MirrorOrigin;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task PublishAsync(
        string feedId,
        string packageName,
        string version,
        Stream tarball,
        JsonObject versionMetadata,
        Uri publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizePackageName(packageName);
        var tarballFileName = GetTarballFileName(normalizedName, version);
        var tarballUrl = BuildTarballUrl(publicBaseUrl, normalizedName, tarballFileName);

        using var sha1 = SHA1.Create();
        await using var hashingStream = new CryptoStream(tarball, sha1, CryptoStreamMode.Read);
        var storagePath = $"{EncodePackagePath(normalizedName)}/-/{tarballFileName}";
        await _blobStore.PutAsync(storagePath, hashingStream, cancellationToken);
        var shasum = Convert.ToHexString(sha1.Hash!).ToLowerInvariant();

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
        dist["tarball"] = tarballUrl;
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

        UpsertDistTag(package, "latest", version, feedId);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Published npm package {Package}@{Version} to feed {FeedId}",
            normalizedName,
            version,
            feedId);
    }

    private void UpsertDistTag(NpmPackage package, string tag, string version, string feedId)
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
    }

    internal static JsonObject BuildPackument(NpmPackage package, Uri publicBaseUrl)
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
            var versionJson = JsonNode.Parse(version.PackumentJson)!.AsObject();
            EnsureDistTarballUrl(versionJson, publicBaseUrl, package.Name);
            versions[version.Version] = versionJson;
        }

        return packument;
    }

    private static void EnsureDistTarballUrl(JsonObject versionJson, Uri publicBaseUrl, string packageName)
    {
        if (versionJson["dist"] is not JsonObject dist)
        {
            return;
        }

        var tarball = dist["tarball"]?.GetValue<string>();
        if (string.IsNullOrEmpty(tarball)
            || tarball.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || tarball.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fileName = tarball.Contains('/') ? Path.GetFileName(tarball) : tarball;
        dist["tarball"] = BuildTarballUrl(publicBaseUrl, packageName, fileName);
    }

    internal static string BuildTarballUrl(Uri publicBaseUrl, string packageName, string tarballFileName)
    {
        var baseUrl = publicBaseUrl.ToString().TrimEnd('/');
        var packagePath = EncodePackagePath(packageName);
        return $"{baseUrl}/{packagePath}/-/{tarballFileName}";
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

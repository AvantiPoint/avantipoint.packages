using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Globalization;
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

public sealed partial class NpmPackageService : INpmPackageService
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> LastMetadataRefresh = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> MetadataRefreshLocks = new();
    private static readonly string[] PackageMetadataProperties =
    [
        "description",
        "keywords",
        "readme",
        "readmeFilename",
        "license",
        "licenses",
        "homepage",
        "repository",
        "bugs",
        "author",
        "maintainers",
        "contributors",
        "funding",
    ];

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

        if (package.Versions.Any(version => version.Origin != PackageOrigin.Published)
            && await RefreshTrackedPackumentIfDueAsync(feedId, normalizedName, cancellationToken))
        {
            package = await _context.NpmPackages
                .AsNoTracking()
                .Include(p => p.Versions)
                .Include(p => p.DistTags)
                .FirstAsync(
                    p => p.FeedId == feedId && p.Name == normalizedName,
                    cancellationToken);
        }

        return package.Versions.Count == 0 ? null : BuildPackument(package, publicBaseUrl);
    }

    public async Task<Stream?> GetTarballAsync(
        string feedId,
        string packageName,
        string tarballFileName,
        Uri publicBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizePackageName(packageName);
        var package = await _context.NpmPackages
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.FeedId == feedId && p.Name == normalizedName, cancellationToken);

        var expectedTarballPath = $"{EncodePackagePath(normalizedName)}/-/{tarballFileName}";

        if (package is null)
        {
            var upstream = await _mirror.FetchPackumentWithPolicyAsync(normalizedName, cancellationToken);
            if (upstream is null)
            {
                return null;
            }

            if (upstream.Strategy == MirrorCachingStrategy.ProxyOnly)
            {
                return await FetchTarballFromPackumentAsync(
                    upstream.Packument,
                    tarballFileName,
                    cancellationToken);
            }

            await PersistUpstreamMetadataAsync(
                feedId,
                normalizedName,
                upstream.Packument,
                upstream.Origin,
                cancellationToken);
            LastMetadataRefresh[GetMetadataRefreshKey(feedId, normalizedName)] = DateTimeOffset.UtcNow;

            package = await _context.NpmPackages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.FeedId == feedId && p.Name == normalizedName, cancellationToken);

            if (package is null)
            {
                return null;
            }
        }

        var version = await _context.NpmVersions
            .FirstOrDefaultAsync(
                v => v.FeedId == feedId
                     && v.PackageKey == package.Key
                     && v.TarballPath == expectedTarballPath,
                cancellationToken);

        if (version is null)
        {
            return null;
        }

        if (await _blobStore.ExistsAsync(version.TarballPath, cancellationToken))
        {
            return await _blobStore.GetAsync(version.TarballPath, cancellationToken);
        }

        return await FetchAndCacheMirroredTarballAsync(version, normalizedName, cancellationToken);
    }

    public async Task<NpmSearchResult> SearchAsync(
        string feedId,
        string? query,
        int from,
        int size,
        CancellationToken cancellationToken = default)
    {
        from = Math.Max(0, from);
        size = Math.Clamp(size, 0, 250);
        var visibleOrigins = Enum.GetValues<PackageOrigin>()
            .Where(origin => _policy.IncludeInDiscovery(FeedProtocol.Npm, origin))
            .ToArray();
        var packages = _context.NpmPackages.AsNoTracking().Where(p => p.FeedId == feedId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            packages = packages.Where(p => EF.Functions.Like(p.Name, $"%{term}%"));
        }

        packages = packages.Where(p => p.Versions.Any(v => visibleOrigins.Contains(v.Origin)));
        var total = await packages.CountAsync(cancellationToken);
        var page = packages
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Key)
            .Skip(from)
            .Take(size);

        // Select one version key per package in SQL, then fetch metadata only for
        // those keys. Separate tagged/fallback subqueries avoid provider-specific
        // correlated ORDER BY behavior while preserving the latest-tag semantics.
        var rows = await page.Select(p => new
        {
            p.Name,
            VersionKey = p.Versions.Where(v => visibleOrigins.Contains(v.Origin)
                    && p.DistTags.Any(t => t.Tag == "latest" && t.Version == v.Version))
                .Select(v => (int?)v.Key).FirstOrDefault()
                ?? p.Versions.Where(v => visibleOrigins.Contains(v.Origin))
                    .OrderByDescending(v => v.Published).ThenByDescending(v => v.Key)
                    .Select(v => (int?)v.Key).FirstOrDefault(),
        }).ToListAsync(cancellationToken);

        var keys = rows.Where(r => r.VersionKey.HasValue).Select(r => r.VersionKey!.Value).ToArray();
        var versions = await _context.NpmVersions.AsNoTracking()
            .Where(v => v.FeedId == feedId && keys.Contains(v.Key))
            .Select(v => new { v.Key, v.Version, v.PackumentJson, v.Published })
            .ToDictionaryAsync(v => v.Key, cancellationToken);

        var objects = rows.Where(r => r.VersionKey.HasValue && versions.ContainsKey(r.VersionKey.Value)).Select(r =>
        {
            var version = versions[r.VersionKey!.Value];
            var meta = JsonNode.Parse(version.PackumentJson)?.AsObject();
            return new NpmSearchObject(
                r.Name,
                version.Version,
                meta?["description"]?.GetValue<string>(),
                version.Published);
        }).ToList();

        return new NpmSearchResult(total, objects);
    }

    private async Task<JsonObject?> TryMirrorPackumentAsync(
        string feedId,
        string normalizedName,
        Uri publicBaseUrl,
        CancellationToken cancellationToken)
    {
        var mirrored = await _mirror.FetchPackumentWithPolicyAsync(normalizedName, cancellationToken);
        if (mirrored is null)
        {
            return null;
        }

        if (mirrored.Strategy == MirrorCachingStrategy.ProxyOnly)
        {
            var packument = mirrored.Packument;
            RewritePackumentTarballUrls(packument, publicBaseUrl, normalizedName);
            return packument;
        }

        var upstream = mirrored.Packument;

        if (upstream["versions"] is not JsonObject)
        {
            return null;
        }

        await PersistUpstreamMetadataAsync(
            feedId,
            normalizedName,
            upstream,
            mirrored.Origin,
            cancellationToken);
        LastMetadataRefresh[GetMetadataRefreshKey(feedId, normalizedName)] = DateTimeOffset.UtcNow;

        var package = await _context.NpmPackages
            .AsNoTracking()
            .Include(p => p.Versions)
            .Include(p => p.DistTags)
            .FirstOrDefaultAsync(
                p => p.FeedId == feedId && p.Name == normalizedName,
                cancellationToken);

        return package is null ? null : BuildPackument(package, publicBaseUrl);
    }

    private async Task<bool> RefreshTrackedPackumentIfDueAsync(
        string feedId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        if (!_npmOptions.Mirror.RefreshTrackedPackages)
        {
            return false;
        }

        var refreshKey = GetMetadataRefreshKey(feedId, normalizedName);
        if (!IsMetadataRefreshDue(refreshKey))
        {
            return false;
        }

        var refreshLock = MetadataRefreshLocks.GetOrAdd(refreshKey, static _ => new SemaphoreSlim(1, 1));
        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsMetadataRefreshDue(refreshKey))
            {
                return false;
            }

            var mirrored = await _mirror.FetchPackumentWithPolicyAsync(normalizedName, cancellationToken);
            LastMetadataRefresh[refreshKey] = DateTimeOffset.UtcNow;
            if (mirrored is null
                || mirrored.Strategy == MirrorCachingStrategy.ProxyOnly
                || mirrored.Packument["versions"] is not JsonObject)
            {
                return false;
            }

            await PersistUpstreamMetadataAsync(
                feedId,
                normalizedName,
                mirrored.Packument,
                mirrored.Origin,
                cancellationToken);
            return true;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private bool IsMetadataRefreshDue(string refreshKey)
    {
        var interval = _npmOptions.Mirror.MetadataRefreshInterval;
        return interval <= TimeSpan.Zero
               || !LastMetadataRefresh.TryGetValue(refreshKey, out var lastRefresh)
               || DateTimeOffset.UtcNow - lastRefresh >= interval;
    }

    private static string GetMetadataRefreshKey(string feedId, string packageName) =>
        $"{feedId}\n{packageName}";

    private async Task<Stream?> FetchTarballFromPackumentAsync(
        JsonObject packument,
        string tarballFileName,
        CancellationToken cancellationToken)
    {
        if (packument["versions"] is not JsonObject versions)
        {
            return null;
        }

        foreach (var entry in versions)
        {
            if (entry.Value is not JsonObject versionJson)
            {
                continue;
            }

            var tarballUrl = versionJson["dist"]?["tarball"]?.GetValue<string>();
            if (string.IsNullOrEmpty(tarballUrl))
            {
                continue;
            }

            if (Uri.TryCreate(tarballUrl, UriKind.Absolute, out var tarballUri)
                && string.Equals(Path.GetFileName(tarballUri.LocalPath), tarballFileName, StringComparison.OrdinalIgnoreCase))
            {
                return await _mirror.FetchTarballAsync(tarballUrl, cancellationToken);
            }
        }

        return null;
    }

    private async Task PersistUpstreamMetadataAsync(
        string feedId,
        string normalizedName,
        JsonObject upstream,
        PackageOrigin origin,
        CancellationToken cancellationToken)
    {
        if (upstream["versions"] is not JsonObject upstreamVersions)
        {
            return;
        }

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

        var upstreamTimes = upstream["time"] as JsonObject;
        var packageMetadataVersion = SelectPackageMetadataVersion(upstream, upstreamVersions, upstreamTimes);
        foreach (var entry in upstreamVersions)
        {
            if (entry.Value is not JsonObject upstreamVersion
                || upstreamVersion["dist"]?["tarball"]?.GetValue<string>() is not { Length: > 0 } tarballUrl)
            {
                continue;
            }

            var versionJson = upstreamVersion.DeepClone().AsObject();
            versionJson["version"] = entry.Key;
            versionJson["name"] = normalizedName;
            if (string.Equals(entry.Key, packageMetadataVersion, StringComparison.OrdinalIgnoreCase))
            {
                // npm keeps README and other package-level fields at the packument root.
                // Store one copy on the preferred version rather than duplicating a potentially
                // large README across every mirrored version.
                CopyPackageMetadata(upstream, versionJson);
            }

            var tarballFileName = GetTarballFileNameFromUrl(tarballUrl);
            if (string.IsNullOrWhiteSpace(tarballFileName))
            {
                continue;
            }

            var storagePath = $"{EncodePackagePath(normalizedName)}/-/{tarballFileName}";
            var shasum = versionJson["dist"]?["shasum"]?.GetValue<string>() ?? string.Empty;
            var versionEntity = package.Versions.FirstOrDefault(v => v.Version == entry.Key);
            if (versionEntity is null)
            {
                versionEntity = new NpmVersion
                {
                    FeedId = feedId,
                    PackageKey = package.Key,
                    Version = entry.Key,
                    TarballPath = storagePath,
                    Shasum = shasum,
                    PackumentJson = versionJson.ToJsonString(JsonOptions),
                    Origin = origin,
                    Published = GetUpstreamPublishedAt(upstreamTimes, entry.Key),
                };
                _context.NpmVersions.Add(versionEntity);
                package.Versions.Add(versionEntity);
            }
            else if (versionEntity.Origin != PackageOrigin.Published)
            {
                versionEntity.TarballPath = storagePath;
                versionEntity.Shasum = shasum;
                versionEntity.PackumentJson = versionJson.ToJsonString(JsonOptions);
                versionEntity.Origin = origin;
                versionEntity.Published = GetUpstreamPublishedAt(upstreamTimes, entry.Key);
            }
        }

        if (upstream["dist-tags"] is JsonObject upstreamTags)
        {
            foreach (var entry in upstreamTags)
            {
                var version = entry.Value?.GetValue<string>();
                if (string.IsNullOrEmpty(version)
                    || package.Versions.All(v => v.Version != version))
                {
                    continue;
                }

                UpsertDistTag(package, entry.Key, version, feedId);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<Stream?> FetchAndCacheMirroredTarballAsync(
        NpmVersion version,
        string packageName,
        CancellationToken cancellationToken)
    {
        if (version.Origin == PackageOrigin.Published
            || JsonNode.Parse(version.PackumentJson) is not JsonObject versionJson
            || versionJson["dist"] is not JsonObject dist
            || dist["tarball"]?.GetValue<string>() is not { Length: > 0 } tarballUrl)
        {
            return null;
        }

        if (await _mirror.GetTarballStrategyAsync(tarballUrl, cancellationToken)
            == MirrorCachingStrategy.ProxyOnly)
        {
            return await _mirror.FetchTarballAsync(tarballUrl, cancellationToken);
        }

        await using var upstream = await _mirror.FetchTarballAsync(tarballUrl, cancellationToken);
        if (upstream is null)
        {
            return null;
        }

        var temporaryTarballPath = Path.GetTempFileName();
        await using var bufferedTarball = new FileStream(
            temporaryTarballPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await upstream.CopyToAsync(bufferedTarball, cancellationToken);

        var hashes = await ComputeTarballHashesAsync(bufferedTarball, cancellationToken);
        if (!MatchesUpstreamIntegrity(dist, hashes))
        {
            _logger.LogWarning(
                "Rejected npm tarball {Package}@{Version} because its upstream integrity metadata did not match",
                packageName,
                version.Version);
            return null;
        }

        bufferedTarball.Position = 0;
        await _blobStore.PutAsync(version.TarballPath, bufferedTarball, cancellationToken);
        version.Shasum = hashes["sha1"].Hex;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cached npm tarball {Package}@{Version} from upstream",
            packageName,
            version.Version);

        return await _blobStore.GetAsync(version.TarballPath, cancellationToken);
    }

    private static DateTime GetUpstreamPublishedAt(JsonObject? upstreamTimes, string version)
    {
        var publishedText = upstreamTimes?[version]?.GetValue<string>();
        return DateTimeOffset.TryParse(
            publishedText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var published)
            ? published.UtcDateTime
            : DateTime.UtcNow;
    }

    private static string? SelectPackageMetadataVersion(
        JsonObject upstream,
        JsonObject upstreamVersions,
        JsonObject? upstreamTimes)
    {
        var latest = upstream["dist-tags"]?["latest"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(latest) && upstreamVersions.ContainsKey(latest))
        {
            return latest;
        }

        return upstreamVersions
            .Select(entry => new
            {
                entry.Key,
                Published = GetUpstreamPublishedAt(upstreamTimes, entry.Key),
            })
            .OrderByDescending(entry => entry.Published)
            .Select(entry => entry.Key)
            .FirstOrDefault();
    }

    private static async Task<IReadOnlyDictionary<string, TarballHash>> ComputeTarballHashesAsync(
        Stream tarball,
        CancellationToken cancellationToken)
    {
        using var sha1 = SHA1.Create();
        using var sha256 = SHA256.Create();
        using var sha384 = SHA384.Create();
        using var sha512 = SHA512.Create();

        tarball.Position = 0;
        var sha1Bytes = await sha1.ComputeHashAsync(tarball, cancellationToken);
        tarball.Position = 0;
        var sha256Bytes = await sha256.ComputeHashAsync(tarball, cancellationToken);
        tarball.Position = 0;
        var sha384Bytes = await sha384.ComputeHashAsync(tarball, cancellationToken);
        tarball.Position = 0;
        var sha512Bytes = await sha512.ComputeHashAsync(tarball, cancellationToken);

        return new Dictionary<string, TarballHash>(StringComparer.OrdinalIgnoreCase)
        {
            ["sha1"] = new(sha1Bytes),
            ["sha256"] = new(sha256Bytes),
            ["sha384"] = new(sha384Bytes),
            ["sha512"] = new(sha512Bytes),
        };
    }

    private static bool MatchesUpstreamIntegrity(
        JsonObject dist,
        IReadOnlyDictionary<string, TarballHash> hashes)
    {
        var integrity = dist["integrity"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(integrity) && !MatchesSubresourceIntegrity(integrity, hashes))
        {
            return false;
        }

        var shasum = dist["shasum"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(shasum)
               || string.Equals(shasum.Trim(), hashes["sha1"].Hex, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesSubresourceIntegrity(
        string integrity,
        IReadOnlyDictionary<string, TarballHash> hashes)
    {
        var candidates = integrity
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token =>
            {
                var separator = token.IndexOf('-');
                var algorithm = separator > 0 ? token[..separator] : string.Empty;
                var digest = separator > 0 ? token[(separator + 1)..] : string.Empty;
                var optionSeparator = digest.IndexOf('?');
                if (optionSeparator >= 0)
                {
                    digest = digest[..optionSeparator];
                }

                return new
                {
                    Algorithm = algorithm,
                    Digest = digest,
                    Rank = algorithm.ToLowerInvariant() switch
                    {
                        "sha512" => 4,
                        "sha384" => 3,
                        "sha256" => 2,
                        "sha1" => 1,
                        _ => 0,
                    },
                };
            })
            .Where(candidate => candidate.Rank > 0 && hashes.ContainsKey(candidate.Algorithm))
            .ToList();

        if (candidates.Count == 0)
        {
            return false;
        }

        var strongestRank = candidates.Max(candidate => candidate.Rank);
        return candidates
            .Where(candidate => candidate.Rank == strongestRank)
            .Any(candidate => string.Equals(
                candidate.Digest,
                hashes[candidate.Algorithm].Base64,
                StringComparison.Ordinal));
    }

    private sealed record TarballHash(byte[] Bytes)
    {
        public string Hex { get; } = Convert.ToHexString(Bytes).ToLowerInvariant();

        public string Base64 { get; } = Convert.ToBase64String(Bytes);
    }

    public async Task PublishAsync(
        string feedId,
        string packageName,
        string version,
        Stream tarball,
        JsonObject versionMetadata,
        Uri publicBaseUrl,
        CancellationToken cancellationToken = default,
        string distTag = "latest")
    {
        var normalizedName = NormalizePackageName(packageName);
        ValidatePackageName(normalizedName);
        ValidateVersion(version);
        ValidateDistTag(distTag);
        var tarballFileName = GetTarballFileName(normalizedName, version);
        var tarballUrl = BuildTarballUrl(publicBaseUrl, normalizedName, tarballFileName);

        using var sha1 = SHA1.Create();
        var temporaryTarballPath = Path.GetTempFileName();
        var storagePath = $"{EncodePackagePath(normalizedName)}/-/{tarballFileName}";
        await using var bufferedTarball = new FileStream(
            temporaryTarballPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await tarball.CopyToWithLimitAsync(bufferedTarball, _npmOptions.MaxTarballBytes, cancellationToken);
        bufferedTarball.Position = 0;
        var hash = await sha1.ComputeHashAsync(bufferedTarball, cancellationToken);
        bufferedTarball.Position = 0;
        var shasum = Convert.ToHexString(hash).ToLowerInvariant();

        // Serialize the existence check and catalog write so competing publishers cannot replace
        // the same version's bytes. No artifact is written until immutability has been checked.
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

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

        if (await _context.NpmVersions.IgnoreQueryFilters().AnyAsync(v => v.FeedId == feedId && v.PackageKey == package.Key && v.Version == version, cancellationToken))
            throw new NpmRegistryException(409, "EPUBLISHCONFLICT", "A package version is immutable. Publish a new version instead.");
        await _blobStore.PutAsync(storagePath, bufferedTarball, cancellationToken);
        var versionJson = versionMetadata.DeepClone().AsObject();
        versionJson["version"] = version;
        versionJson["name"] = normalizedName;

        var dist = versionJson["dist"] as JsonObject ?? new JsonObject();
        dist["shasum"] = shasum;
        dist["tarball"] = tarballUrl;
        versionJson["dist"] = dist;

        var versionEntity = new NpmVersion
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

        UpsertDistTag(package, distTag, version, feedId);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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
            ["_id"] = package.Name,
            ["_rev"] = GetRevision(package),
            ["dist-tags"] = new JsonObject(),
            ["versions"] = new JsonObject(),
        };

        var distTags = (JsonObject)packument["dist-tags"]!;
        foreach (var tag in package.DistTags)
        {
            distTags[tag.Tag] = tag.Version;
        }

        var latestVersion = package.DistTags
            .FirstOrDefault(tag => string.Equals(tag.Tag, "latest", StringComparison.OrdinalIgnoreCase))
            is { } latestTag
                ? package.Versions.FirstOrDefault(version =>
                    string.Equals(version.Version, latestTag.Version, StringComparison.OrdinalIgnoreCase))
                : null;
        var metadataVersion = latestVersion ?? package.Versions.OrderByDescending(version => version.Published).FirstOrDefault();
        if (metadataVersion is not null
            && JsonNode.Parse(metadataVersion.PackumentJson) is JsonObject packageMetadata)
        {
            CopyPackageMetadata(packageMetadata, packument);
        }

        var versions = (JsonObject)packument["versions"]!;
        foreach (var version in package.Versions)
        {
            var versionJson = JsonNode.Parse(version.PackumentJson)!.AsObject();
            EnsureDistTarballUrl(versionJson, publicBaseUrl, package.Name, forceRewrite: true);
            versions[version.Version] = versionJson;
        }

        return packument;
    }

    internal static void CopyPackageMetadata(
        JsonObject source,
        JsonObject destination,
        bool overwriteExisting = false)
    {
        foreach (var propertyName in PackageMetadataProperties)
        {
            if (source[propertyName] is not { } value
                || (!overwriteExisting && destination[propertyName] is not null))
            {
                continue;
            }

            destination[propertyName] = value.DeepClone();
        }
    }

    private static void RewritePackumentTarballUrls(JsonObject? packument, Uri publicBaseUrl, string packageName)
    {
        if (packument?["versions"] is not JsonObject versions)
        {
            return;
        }

        foreach (var entry in versions)
        {
            if (entry.Value is JsonObject versionJson)
            {
                EnsureDistTarballUrl(versionJson, publicBaseUrl, packageName, forceRewrite: true);
            }
        }
    }

    private static void EnsureDistTarballUrl(
        JsonObject versionJson,
        Uri publicBaseUrl,
        string packageName,
        bool forceRewrite = false)
    {
        if (versionJson["dist"] is not JsonObject dist)
        {
            return;
        }

        var tarball = dist["tarball"]?.GetValue<string>();
        if (string.IsNullOrEmpty(tarball))
        {
            return;
        }

        if (!forceRewrite
            && (tarball.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || tarball.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var fileName = GetTarballFileNameFromUrl(tarball);
        dist["tarball"] = BuildTarballUrl(publicBaseUrl, packageName, fileName);
    }

    private static string GetTarballFileNameFromUrl(string tarball)
    {
        if (Uri.TryCreate(tarball, UriKind.Absolute, out var uri))
        {
            return Path.GetFileName(uri.AbsolutePath);
        }

        return tarball.Contains('/') ? Path.GetFileName(tarball) : tarball;
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

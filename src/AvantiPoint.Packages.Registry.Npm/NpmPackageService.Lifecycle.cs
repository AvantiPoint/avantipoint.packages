using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Npm;
using Microsoft.EntityFrameworkCore;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Registry.Npm;

public sealed class NpmRegistryException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed partial class NpmPackageService
{
    public async Task SetDistTagAsync(string feedId, string packageName, string tag, string? version, CancellationToken cancellationToken = default)
    {
        ValidateDistTag(tag);
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var package = await GetLocalPackageForMutationAsync(feedId, packageName, cancellationToken);
        if (version is null)
        {
            var existing = package.DistTags.SingleOrDefault(t => t.Tag == tag);
            if (existing is null) throw new NpmRegistryException(404, "E404", "This dist-tag does not exist.");
            _context.NpmDistTags.Remove(existing);
        }
        else
        {
            if (!package.Versions.Any(v => v.Version == version))
                throw new NpmRegistryException(404, "E404", "The target package version does not exist.");
            UpsertDistTag(package, tag, version, feedId);
        }
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateDeprecationsAsync(string feedId, string packageName, JsonObject packument, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var package = await GetLocalPackageForMutationAsync(feedId, packageName, cancellationToken);
        if (packument["_rev"] is not JsonValue revision || !revision.TryGetValue<string>(out var expectedRevision)
            || expectedRevision != GetRevision(package))
            throw new NpmRegistryException(409, "E409", "Package metadata has changed. Fetch it again before updating.");
        if (packument["versions"] is not JsonObject versions || versions.Count != package.Versions.Count
            || versions.Any(v => !package.Versions.Any(stored => stored.Version == v.Key)))
            throw new NpmRegistryException(400, "E400", "Metadata updates must preserve the published versions.");
        foreach (var stored in package.Versions)
        {
            if (versions[stored.Version] is not JsonObject metadata)
                throw new NpmRegistryException(400, "E400", "Invalid version metadata.");
            if (metadata.TryGetPropertyValue("deprecated", out var value))
            {
                if (value is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var message) || message.Length > 4096)
                    throw new NpmRegistryException(400, "E400", "A deprecation message must be a string of at most 4096 characters.");
                // Never let a metadata-only request alter tarball URLs, hashes, dependencies or versions.
                var existing = JsonNode.Parse(stored.PackumentJson)!.AsObject();
                existing["deprecated"] = message;
                stored.PackumentJson = existing.ToJsonString(JsonOptions);
            }
        }
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<NpmPackage> GetLocalPackageForMutationAsync(string feedId, string packageName, CancellationToken cancellationToken)
    {
        var name = NormalizePackageName(packageName);
        ValidatePackageName(name);
        var package = await _context.NpmPackages.Include(p => p.Versions).Include(p => p.DistTags)
            .SingleOrDefaultAsync(p => p.FeedId == feedId && p.Name == name, cancellationToken)
            ?? throw new NpmRegistryException(404, "E404", "The package does not exist on this feed.");
        if (package.Versions.Any(v => v.Origin != PackageOrigin.Published))
            throw new NpmRegistryException(403, "E403", "Upstream package metadata is managed by its source registry.");
        return package;
    }

    private static string GetRevision(NpmPackage package)
    {
        var metadata = JsonSerializer.Serialize(new
        {
            versions = package.Versions.OrderBy(v => v.Version, StringComparer.Ordinal).Select(v => new { v.Version, v.PackumentJson }),
            tags = package.DistTags.OrderBy(t => t.Tag, StringComparer.Ordinal).Select(t => new { t.Tag, t.Version }),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(metadata))).ToLowerInvariant();
    }

    private static void ValidatePackageName(string name)
    {
        if (name.Length > 214 || !Regex.IsMatch(name, @"\A(?:@[a-z0-9][a-z0-9._-]*/)?[a-z0-9][a-z0-9._-]*\z", RegexOptions.CultureInvariant))
            throw new NpmRegistryException(400, "E400", "Invalid npm package name.");
    }
    private static void ValidateVersion(string version)
    {
        if (version.Length > 64 || !Regex.IsMatch(version, @"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant))
            throw new NpmRegistryException(400, "E400", "A valid semantic package version is required.");
    }
    private static void ValidateDistTag(string tag)
    {
        if (tag.Length > 128 || !Regex.IsMatch(tag, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]*\z", RegexOptions.CultureInvariant)
            || NuGetVersion.TryParse(tag, out _))
            throw new NpmRegistryException(400, "E400", "A dist-tag must be a name, not a version number.");
    }
}

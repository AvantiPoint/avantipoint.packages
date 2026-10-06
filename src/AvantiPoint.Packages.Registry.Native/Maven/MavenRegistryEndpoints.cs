using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Authentication;
using AvantiPoint.Feed.Platform.Callbacks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Native.Authentication;
using AvantiPoint.Packages.Registry.Native.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace AvantiPoint.Packages.Registry.Native.Maven;

public static class MavenRegistryEndpoints
{
    public static void MapMavenRegistry(this WebApplication app, string prefix)
    {
        var group = app.MapGroup(prefix);
        group.MapMethods("/{**path}", ["GET", "HEAD"], Download)
            .AddEndpointFilter(new NativeAuthorizationFilter(FeedProtocol.Maven, FeedOperation.Pull));
        group.MapPut("/{**path}", Publish)
            .AddEndpointFilter(new NativeAuthorizationFilter(FeedProtocol.Maven, FeedOperation.Push));
    }

    private static async Task<IResult> Download(string path, HttpContext http, NativeArtifactStore store,
        ISurfaceContextAccessor accessor, IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!MavenArtifactPath.TryParse(path, out var parsed)) return Results.NotFound();
        var item = parsed!;
        var surface = accessor.Current!;
        if (handler is not null && !await handler.CanAccessArtifact(new(surface, item.PackageName, item.Version, path), ct))
            return Results.StatusCode(403);
        if (item.IsMetadata)
        {
            var metadata = await MetadataAsync(item, surface, store, ct);
            if (metadata is null) return Results.NotFound();
            return item.ChecksumAlgorithm is null
                ? Results.Bytes(metadata, "application/xml")
                : Results.Text(Hash(metadata, item.ChecksumAlgorithm), "text/plain");
        }
        var artifact = await store.FindAsync(surface, item.Path, ct);
        if (artifact is null) return Results.NotFound();
        if (item.ChecksumAlgorithm is not null)
        {
            await using var stream = await store.OpenAsync(artifact, ct);
            using var hash = IncrementalHash.CreateHash(new HashAlgorithmName(item.ChecksumAlgorithm.ToUpperInvariant()));
            var buffer = new byte[81920];
            int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0) hash.AppendData(buffer, 0, count);
            return Results.Text(Convert.ToHexStringLower(hash.GetHashAndReset()), "text/plain");
        }
        return await NativeArtifactResponses.DownloadAsync(http, surface, artifact, store, handler, ct);
    }

    private static async Task<IResult> Publish(string path, HttpContext http, NativeArtifactStore store,
        ISurfaceContextAccessor accessor, IOptionsMonitor<NativeRegistryOptions> options,
        IFeedActionHandler? handler, CancellationToken ct)
    {
        if (!MavenArtifactPath.TryParse(path, out var parsed))
            return Results.BadRequest(new { error = "Invalid Maven release path; snapshots are not supported." });
        var item = parsed!;
        var surface = accessor.Current!;
        var evt = new FeedArtifactEventContext(surface, item.PackageName, item.Version, item.Path);
        if (handler is not null && !await handler.CanAccessArtifact(evt, ct)) return Results.StatusCode(403);
        var limit = item.IsMetadata || item.ChecksumAlgorithm is not null ? 1024 * 1024
            : options.Get("Maven").MaxArtifactBytes;
        if (http.Request.ContentLength > limit) return Results.StatusCode(413);
        try
        {
            await using var upload = await ArtifactUpload.ReadAsync(http.Request.Body, limit, ct);
            if (upload.Length == 0) return Results.BadRequest(new { error = "Empty artifact." });
            if (item.ChecksumAlgorithm is not null)
            {
                using var reader = new StreamReader(upload.Stream, leaveOpen: true);
                var expected = (await reader.ReadToEndAsync(ct)).Trim();
                var size = item.ChecksumAlgorithm switch { "md5" => 32, "sha1" => 40, "sha256" => 64, _ => 128 };
                if (expected.Length != size || !expected.All(Uri.IsHexDigit)) return Results.BadRequest();
                // Metadata is generated from committed releases. Client-authored
                // metadata and its sidecars are hints, never the authoritative index.
                if (item.IsMetadata) return Results.StatusCode(201);
                var existing = await store.FindAsync(surface, item.Path, ct);
                if (existing is null) return Results.NotFound();
                await using var bytes = await store.OpenAsync(existing, ct);
                using HashAlgorithm hash = item.ChecksumAlgorithm switch
                {
                    "md5" => MD5.Create(), "sha1" => SHA1.Create(),
                    "sha256" => SHA256.Create(), _ => SHA512.Create(),
                };
                var actual = Convert.ToHexStringLower(await hash.ComputeHashAsync(bytes, ct));
                return actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
                    ? Results.StatusCode(201) : Results.BadRequest(new { error = "Checksum does not match the artifact." });
            }
            if (item.IsMetadata || item.Path.EndsWith(".pom", StringComparison.Ordinal))
            {
                using var xml = XmlReader.Create(upload.Stream, new XmlReaderSettings
                {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = 1024 * 1024,
                });
                var document = XDocument.Load(xml);
                var root = document.Root;
                string? Value(string name) => root?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
                if (root?.Name.LocalName != (item.IsMetadata ? "metadata" : "project")
                    || Value("artifactId") != item.ArtifactId || Value("groupId") != item.GroupId
                    || (!item.IsMetadata && Value("version") != item.Version))
                    return Results.BadRequest(new { error = "Maven metadata does not match its coordinates." });
                if (item.IsMetadata) return Results.StatusCode(201);
            }
            if (item.Path.EndsWith(".module", StringComparison.Ordinal))
            {
                using var json = await JsonDocument.ParseAsync(upload.Stream, cancellationToken: ct);
                if (!json.RootElement.TryGetProperty("component", out var component)
                    || component.GetProperty("group").GetString() != item.GroupId
                    || component.GetProperty("module").GetString() != item.ArtifactId
                    || component.GetProperty("version").GetString() != item.Version)
                    return Results.BadRequest(new { error = "Gradle metadata does not match its coordinates." });
            }
            var type = item.Path.EndsWith(".pom", StringComparison.Ordinal) ? "application/xml"
                : item.Path.EndsWith(".module", StringComparison.Ordinal) ? "application/json" : "application/octet-stream";
            var result = await store.PutAsync(surface, item.Path, item.PackageName, item.Version, type, upload, null, ct);
            if (result == StoragePutResult.Conflict) return Results.Conflict(new { error = "Released artifacts are immutable." });
            if (result == StoragePutResult.Success && handler is not null) await handler.OnArtifactUploaded(evt, ct);
            return Results.StatusCode(result == StoragePutResult.Success ? 201 : 200);
        }
        catch (ArtifactTooLargeException) { return Results.StatusCode(413); }
        catch (Exception ex) when (ex is XmlException or JsonException or KeyNotFoundException or InvalidOperationException)
        { return Results.BadRequest(new { error = "Invalid Maven metadata." }); }
    }

    private static async Task<byte[]?> MetadataAsync(MavenArtifactPath path, SurfaceContext surface,
        NativeArtifactStore store, CancellationToken ct)
    {
        var artifacts = await store.ListAsync(surface, path.PackageName, ct);
        var versions = artifacts.Where(a => a.Path.EndsWith(".pom", StringComparison.Ordinal))
            .Select(a => a.Version).Distinct().OrderBy(v => NuGetVersion.TryParse(v, out var n) ? n : new NuGetVersion(0, 0, 0))
            .ThenBy(v => v, StringComparer.Ordinal).ToArray();
        if (versions.Length == 0) return null;
        var latest = versions[^1];
        var doc = new XElement("metadata", new XElement("groupId", path.GroupId), new XElement("artifactId", path.ArtifactId),
            new XElement("versioning", new XElement("latest", latest), new XElement("release", latest),
                new XElement("versions", versions.Select(v => new XElement("version", v))),
                new XElement("lastUpdated", artifacts.Max(a => a.PublishedUtc).ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture))));
        return Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
    }

    private static string Hash(byte[] bytes, string algorithm)
    {
        using var hash = IncrementalHash.CreateHash(new HashAlgorithmName(algorithm.ToUpperInvariant()));
        hash.AppendData(bytes);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

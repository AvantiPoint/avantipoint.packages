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
        var evt = new FeedArtifactEventContext(surface, item.PackageName, item.Version, path);
        if (handler is not null && !await handler.CanAccessArtifact(evt, ct))
            return Results.StatusCode(403);
        if (item.IsMetadata)
        {
            var metadata = await MetadataAsync(item, surface, store, handler, ct);
            if (metadata is null) return Results.NotFound();
            return item.ChecksumAlgorithm is null
                ? Results.Bytes(metadata, "application/xml")
                : Results.Text(Hash(metadata, item.ChecksumAlgorithm), "text/plain");
        }
        var artifact = await store.FindAsync(surface, item.Path, ct);
        if (artifact is null) return Results.NotFound();
        if (item.ChecksumAlgorithm is not null)
        {
            return Results.Text(await store.GetChecksumAsync(artifact, item.ChecksumAlgorithm, ct), "text/plain");
        }
        return await NativeArtifactResponses.DownloadAsync(http, surface, artifact, store, handler, ct, authorizedEvent: evt);
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
        // Metadata paths can denote either an artifact or a plugin group. Resolve
        // XML coordinates before authorizing, rather than guessing an artifact ID.
        if ((!item.IsMetadata || item.ChecksumAlgorithm is not null)
            && handler is not null && !await handler.CanAccessArtifact(evt, ct))
            return Results.StatusCode(403);
        var limit = item.IsMetadata || item.ChecksumAlgorithm is not null
            || item.Path.EndsWith(".pom", StringComparison.Ordinal) || item.Path.EndsWith(".module", StringComparison.Ordinal)
            ? 1024 * 1024
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
                var actual = await store.GetChecksumAsync(existing, item.ChecksumAlgorithm, ct);
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
                var parent = item.IsMetadata ? null : root?.Elements().FirstOrDefault(e => e.Name.LocalName == "parent");
                string? ParentValue(string name) => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
                var group = Value("groupId") ?? ParentValue("groupId");
                var version = Value("version") ?? ParentValue("version");
                if (item.IsMetadata && root?.Name.LocalName == "metadata"
                    && !root.Elements().Any(element => element.Name.LocalName == "artifactId"))
                {
                    return await AcceptPluginMetadataHintAsync(root, item, surface, handler, ct);
                }
                if (root?.Name.LocalName != (item.IsMetadata ? "metadata" : "project")
                    || string.IsNullOrEmpty(item.GroupId)
                    || Value("artifactId") != item.ArtifactId || group != item.GroupId
                    || (!item.IsMetadata && version != item.Version))
                    return Results.BadRequest(new { error = "Maven metadata does not match its coordinates." });
                if (item.IsMetadata)
                {
                    if (handler is not null && !await handler.CanAccessArtifact(evt, ct))
                        return Results.StatusCode(403);
                    return Results.StatusCode(201);
                }
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
            await store.NotifyUploadedAsync(surface, item.Path, handler, ct);
            return Results.StatusCode(result == StoragePutResult.Success ? 201 : 200);
        }
        catch (ArtifactTooLargeException) { return Results.StatusCode(413); }
        catch (Exception ex) when (ex is XmlException or JsonException or KeyNotFoundException or InvalidOperationException)
        { return Results.BadRequest(new { error = "Invalid Maven metadata." }); }
    }

    private static async Task<IResult> AcceptPluginMetadataHintAsync(XElement root,
        MavenArtifactPath path, SurfaceContext surface, IFeedActionHandler? handler, CancellationToken ct)
    {
        var group = path.Path[..^"/maven-metadata.xml".Length].Replace('/', '.');
        var groupElements = root.Elements().Where(element => element.Name.LocalName == "groupId").ToArray();
        var containers = root.Elements().Where(element => element.Name.LocalName == "plugins").ToArray();
        if (groupElements.Length != 1 || groupElements[0].Value != group || containers.Length != 1
            || root.Elements().Any(element => element.Name.LocalName is "version" or "versioning"))
            return Results.BadRequest(new { error = "Maven plugin metadata does not match its group." });

        var plugins = containers[0].Elements().ToArray();
        if (plugins.Length == 0 || plugins.Any(plugin => plugin.Name.LocalName != "plugin"))
            return Results.BadRequest(new { error = "Invalid Maven plugin metadata." });
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plugin in plugins)
        {
            var artifacts = plugin.Elements().Where(element => element.Name.LocalName == "artifactId").ToArray();
            var prefixes = plugin.Elements().Where(element => element.Name.LocalName == "prefix").ToArray();
            if (artifacts.Length != 1 || prefixes.Length != 1
                || !ArtifactPath.IsValidSegment(artifacts[0].Value)
                || !ArtifactPath.IsValidSegment(prefixes[0].Value)
                || (group + ":" + artifacts[0].Value).Length > 256)
                return Results.BadRequest(new { error = "Invalid Maven plugin metadata." });
            identities.Add(group + ":" + artifacts[0].Value);
        }
        foreach (var identity in identities)
        {
            var evt = new FeedArtifactEventContext(surface, identity, null, path.Path);
            if (handler is not null && !await handler.CanAccessArtifact(evt, ct))
                return Results.StatusCode(403);
        }
        // Like artifact-level metadata, this is a bounded compatibility hint.
        // It never creates a release, changes ownership, or replaces the index.
        return Results.StatusCode(201);
    }

    private static async Task<byte[]?> MetadataAsync(MavenArtifactPath path, SurfaceContext surface,
        NativeArtifactStore store, IFeedActionHandler? handler, CancellationToken ct)
    {
        var artifacts = await store.ListAsync(surface, path.PackageName, ct);
        var directory = path.Path[..^"maven-metadata.xml".Length];
        // A classifier/alternate extension POM cannot resolve the version's
        // descriptor. Advertise only the exact file consumers will request.
        var descriptors = await NativeArtifactResponses.FilterAccessibleAsync(surface,
            artifacts.Where(a => a.Version is not null
                && a.Path == $"{directory}{a.Version}/{path.ArtifactId}-{a.Version}.pom"), handler, ct);
        var versions = descriptors
            .Select(a => a.Version).Distinct().OrderBy(v => v, MavenVersionComparer.Instance)
            .ThenBy(v => v, StringComparer.Ordinal).ToArray();
        if (versions.Length == 0) return null;
        var latest = versions[^1];
        var doc = new XElement("metadata", new XElement("groupId", path.GroupId), new XElement("artifactId", path.ArtifactId),
            new XElement("versioning", new XElement("latest", latest), new XElement("release", latest),
                new XElement("versions", versions.Select(v => new XElement("version", v))),
                new XElement("lastUpdated", descriptors.Max(a => a.PublishedUtc).ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture))));
        return Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
    }

    private static string Hash(byte[] bytes, string algorithm)
    {
        using var hash = IncrementalHash.CreateHash(new HashAlgorithmName(algorithm.ToUpperInvariant()));
        hash.AppendData(bytes);
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

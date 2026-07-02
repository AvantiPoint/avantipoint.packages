using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Host.Admin.Services.Publishers;

/// <summary>
/// Publishes locally hosted npm packages to an external npm registry (for example npmjs.org)
/// using the standard npm publish protocol: a PUT of the packument fragment with the tarball
/// as a base64 <c>_attachments</c> entry, authenticated with a bearer token.
/// </summary>
public sealed class NpmDownstreamPublisher(
    IContext context,
    IStorageBackendFactory storageFactory,
    ISecretProtector secretProtector,
    IHttpClientFactory httpClientFactory,
    ILogger<NpmDownstreamPublisher> logger) : IDownstreamPublisher
{
    public PublishTargetProtocol Protocol => PublishTargetProtocol.Npm;

    public async Task<bool> PushAsync(
        string packageId,
        string? version,
        HostPublishTarget target,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = packageId.ToLowerInvariant();
        var npmVersion = await context.NpmVersions
            .AsNoTracking()
            .Include(v => v.Package)
            .Where(v => v.Package.Name == normalizedName && v.Origin == PackageOrigin.Published)
            .Where(v => version == null || v.Version == version)
            .OrderByDescending(v => v.Published)
            .FirstOrDefaultAsync(cancellationToken);

        if (npmVersion is null)
        {
            logger.LogWarning("npm package {Package} {Version} not found locally", packageId, version ?? "(latest)");
            return false;
        }

        var blobStore = storageFactory.CreatePathStore("npm/");
        Stream? tarball;
        try
        {
            // File-backed storage throws instead of returning null for a missing blob (matching
            // PathBlobStore.ExistsAsync's own catch clauses below) - a single package's tarball
            // going missing must fail only that package, not abort the whole group promotion.
            tarball = await blobStore.GetAsync(npmVersion.TarballPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            tarball = null;
        }
        catch (DirectoryNotFoundException)
        {
            tarball = null;
        }

        if (tarball is null)
        {
            logger.LogWarning("npm tarball missing for {Package} {Version}", normalizedName, npmVersion.Version);
            return false;
        }

        await using var _ = tarball;

        using var buffer = new MemoryStream();
        await tarball.CopyToAsync(buffer, cancellationToken);
        var tarballBytes = buffer.ToArray();

        var versionJson = JsonNode.Parse(npmVersion.PackumentJson)?.AsObject() ?? [];
        versionJson["name"] = normalizedName;
        versionJson["version"] = npmVersion.Version;

        var tarballFileName = $"{normalizedName.Split('/')[^1]}-{npmVersion.Version}.tgz";
        var endpoint = target.PublishEndpoint.TrimEnd('/');
        var dist = versionJson["dist"]?.AsObject() ?? [];
        dist["tarball"] = $"{endpoint}/{EncodePackagePath(normalizedName)}/-/{tarballFileName}";
        dist["shasum"] = npmVersion.Shasum;
        versionJson["dist"] = dist.DeepClone();

        var publishBody = new JsonObject
        {
            ["_id"] = normalizedName,
            ["name"] = normalizedName,
            ["versions"] = new JsonObject { [npmVersion.Version] = versionJson.DeepClone() },
            ["dist-tags"] = new JsonObject { ["latest"] = npmVersion.Version },
            ["_attachments"] = new JsonObject
            {
                [tarballFileName] = new JsonObject
                {
                    ["content_type"] = "application/octet-stream",
                    ["data"] = Convert.ToBase64String(tarballBytes),
                    ["length"] = tarballBytes.Length,
                },
            },
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"{endpoint}/{EncodePackagePath(normalizedName)}")
        {
            Content = new StringContent(publishBody.ToJsonString(), Encoding.UTF8, "application/json"),
        };

        var token = secretProtector.Unprotect(target.ApiToken);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var client = httpClientFactory.CreateClient(nameof(NpmDownstreamPublisher));
        HttpResponseMessage response;
        try
        {
            // A publish failing due to the downstream target being unreachable (DNS/TLS/timeout)
            // must fail only this package, like an HTTP error status does below - not throw out of
            // PushAsync and abort the caller's whole group promotion loop.
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to reach npm target {Target} for {Package} {Version}", target.Name, normalizedName, npmVersion.Version);
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Timed out publishing npm package {Package} {Version} to {Target}", normalizedName, npmVersion.Version, target.Name);
            return false;
        }

        using var disposeResponse = response;
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Failed to publish npm package {Package} {Version} to {Target}: {StatusCode}",
                normalizedName,
                npmVersion.Version,
                target.Name,
                (int)response.StatusCode);
            return false;
        }

        logger.LogInformation(
            "Published npm package {Package} {Version} to {Target}",
            normalizedName,
            npmVersion.Version,
            target.Name);
        return true;
    }

    private static string EncodePackagePath(string normalizedName) =>
        normalizedName.Replace("/", "%2F");
}

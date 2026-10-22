using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Registry.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

/// <summary>
/// Exercises two real HTTP feeds: an authenticated upstream registry and a downstream
/// pull-through feed. These tests intentionally use individual repository APIs only;
/// no upstream catalog endpoint participates in refresh.
/// </summary>
public sealed class AuthenticatedUpstreamRefreshIntegrationTests
{
    [Fact]
    public async Task DockerPull_RefreshesConsumedMutableTag_WithoutCachingUnrelatedArtifactsOrLayers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var upstreamToken = $"oci-upstream-{Guid.NewGuid():N}";
        var repository = $"images/tracked-{Guid.NewGuid():N}";
        var unrelatedRepository = $"images/unrelated-{Guid.NewGuid():N}";

        await using var upstream = await FeedTestServerHost.StartAsync(
            apiKey: upstreamToken,
            requireAuthentication: true);
        using var anonymousClient = new HttpClient { BaseAddress = upstream.BaseAddress };
        upstream.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", upstreamToken);

        var firstLayer = Encoding.UTF8.GetBytes("docker-layer-v1");
        var secondLayer = Encoding.UTF8.GetBytes("docker-layer-v2");
        var first = await PushArtifactAsync(
            upstream.Client,
            "/v2",
            repository,
            "latest",
            firstLayer,
            isHelm: false,
            cancellationToken);
        await PushSimpleManifestAsync(
            upstream.Client,
            "/v2",
            unrelatedRepository,
            "latest",
            "unrelated",
            cancellationToken);

        var anonymous = await anonymousClient.GetAsync(
            $"/v2/{repository}/manifests/latest",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        await using var downstream = await FeedTestServerHost.StartAsync(
            configure: configuration => configuration.AddInMemoryCollection(
                MirrorConfiguration(
                    "Default",
                    upstream.BaseAddress.ToString(),
                    upstreamToken,
                    profile: "ContainerImages")));

        var firstPull = await downstream.Client.GetAsync(
            $"/v2/{repository}/manifests/latest",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, firstPull.StatusCode);
        Assert.Equal(first.ManifestDigest, ContentDigest(firstPull));

        var second = await PushArtifactAsync(
            upstream.Client,
            "/v2",
            repository,
            "latest",
            secondLayer,
            isHelm: false,
            cancellationToken);
        var refreshedPull = await downstream.Client.GetAsync(
            $"/v2/{repository}/manifests/latest",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshedPull.StatusCode);
        Assert.Equal(second.ManifestDigest, ContentDigest(refreshedPull));
        Assert.NotEqual(first.ManifestDigest, second.ManifestDigest);
        await AssertRepositoryNotTrackedAsync(downstream, unrelatedRepository, ociSegment: null, cancellationToken);
        await AssertBlobStoredAsync(downstream, second.LayerDigest, ociSegment: null, expected: false, cancellationToken);

        var layerPull = await downstream.Client.GetAsync(
            $"/v2/{repository}/blobs/{second.LayerDigest}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, layerPull.StatusCode);
        Assert.Equal(secondLayer, await layerPull.Content.ReadAsByteArrayAsync(cancellationToken));
        await AssertBlobStoredAsync(downstream, second.LayerDigest, ociSegment: null, expected: true, cancellationToken);
    }

    [Fact]
    public async Task HelmTags_DiscoversNewVersionForConsumedChart_WithoutCachingChartLayer()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var upstreamToken = $"helm-upstream-{Guid.NewGuid():N}";
        var repository = $"tracked-{Guid.NewGuid():N}";
        var unrelatedRepository = $"unrelated-{Guid.NewGuid():N}";

        await using var upstream = await FeedTestServerHost.StartAsync(
            apiKey: upstreamToken,
            requireAuthentication: true);
        using var anonymousClient = new HttpClient { BaseAddress = upstream.BaseAddress };
        upstream.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", upstreamToken);

        var firstChart = Encoding.UTF8.GetBytes("helm-chart-v1");
        await PushArtifactAsync(
            upstream.Client,
            "/helm/v2",
            repository,
            "1.0.0",
            firstChart,
            isHelm: true,
            cancellationToken);
        await PushSimpleManifestAsync(
            upstream.Client,
            "/helm/v2",
            unrelatedRepository,
            "9.9.9",
            "unrelated-helm",
            cancellationToken);

        var anonymous = await anonymousClient.GetAsync(
            $"/helm/v2/{repository}/manifests/1.0.0",
            cancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var upstreamHelmUrl = new Uri(upstream.BaseAddress, "helm").ToString();
        await using var downstream = await FeedTestServerHost.StartAsync(
            configure: configuration => configuration.AddInMemoryCollection(
                MirrorConfiguration(
                    "Helm",
                    upstreamHelmUrl,
                    upstreamToken,
                    profile: "HelmCharts")));

        var consumed = await downstream.Client.GetAsync(
            $"/helm/v2/{repository}/manifests/1.0.0",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, consumed.StatusCode);

        var secondChart = Encoding.UTF8.GetBytes("helm-chart-v2");
        var second = await PushArtifactAsync(
            upstream.Client,
            "/helm/v2",
            repository,
            "1.1.0",
            secondChart,
            isHelm: true,
            cancellationToken);

        var tagsResponse = await downstream.Client.GetAsync(
            $"/helm/v2/{repository}/tags/list",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, tagsResponse.StatusCode);
        using var tagsJson = JsonDocument.Parse(await tagsResponse.Content.ReadAsStringAsync(cancellationToken));
        var tags = tagsJson.RootElement.GetProperty("tags")
            .EnumerateArray()
            .Select(static tag => tag.GetString())
            .ToArray();
        Assert.Contains("1.0.0", tags);
        Assert.Contains("1.1.0", tags);

        var discoveredManifest = await downstream.Client.GetAsync(
            $"/helm/v2/{repository}/manifests/{second.ManifestDigest}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, discoveredManifest.StatusCode);
        await AssertRepositoryNotTrackedAsync(downstream, unrelatedRepository, "helm", cancellationToken);
        await AssertBlobStoredAsync(downstream, second.LayerDigest, "helm", expected: false, cancellationToken);

        var chartPull = await downstream.Client.GetAsync(
            $"/helm/v2/{repository}/blobs/{second.LayerDigest}",
            cancellationToken);
        Assert.Equal(HttpStatusCode.OK, chartPull.StatusCode);
        Assert.Equal(secondChart, await chartPull.Content.ReadAsByteArrayAsync(cancellationToken));
        await AssertBlobStoredAsync(downstream, second.LayerDigest, "helm", expected: true, cancellationToken);
    }

    private static Dictionary<string, string?> MirrorConfiguration(
        string surface,
        string upstreamUrl,
        string upstreamToken,
        string profile) =>
        new()
        {
            [$"Feed:Oci:{surface}:Enabled"] = "true",
            [$"Feed:Oci:{surface}:Profile"] = profile,
            [$"Feed:Oci:{surface}:IncludeMirroredInCatalog"] = "true",
            [$"Feed:Oci:{surface}:Mirror:MetadataRefreshInterval"] = "00:00:00",
            [$"Feed:Oci:{surface}:Mirror:RefreshTrackedArtifacts"] = "true",
            [$"Feed:Oci:{surface}:Mirror:TrackedTagDiscoveryLimit"] = "32",
            [$"Feed:Oci:{surface}:Mirror:NewHelmVersionsPerRefresh"] = "4",
            [$"Feed:Oci:{surface}:Mirror:Registries:0:Url"] = upstreamUrl,
            [$"Feed:Oci:{surface}:Mirror:Registries:0:Username"] = "mirror-client",
            [$"Feed:Oci:{surface}:Mirror:Registries:0:Password"] = upstreamToken,
            [$"Feed:Oci:{surface}:Mirror:Registries:0:Priority"] = "0",
            [$"Feed:Oci:{surface}:Mirror:Registries:0:CachingStrategy"] = "IndexAndCache",
        };

    private static async Task<PushedArtifact> PushArtifactAsync(
        HttpClient client,
        string apiRoot,
        string repository,
        string tag,
        byte[] layer,
        bool isHelm,
        CancellationToken cancellationToken)
    {
        var config = Encoding.UTF8.GetBytes(isHelm ? "{\"apiVersion\":\"v2\"}" : "{\"architecture\":\"amd64\",\"os\":\"linux\"}");
        var configDigest = await PushBlobAsync(client, apiRoot, repository, config, cancellationToken);
        var layerDigest = await PushBlobAsync(client, apiRoot, repository, layer, cancellationToken);
        var manifestBytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                mediaType = "application/vnd.oci.image.manifest.v1+json",
                config = new
                {
                    mediaType = isHelm
                        ? "application/vnd.cncf.helm.config.v1+json"
                        : "application/vnd.oci.image.config.v1+json",
                    size = config.Length,
                    digest = configDigest,
                },
                layers = new[]
                {
                    new
                    {
                        mediaType = isHelm
                            ? "application/vnd.cncf.helm.chart.content.v1.tar+gzip"
                            : "application/vnd.oci.image.layer.v1.tar+gzip",
                        size = layer.Length,
                        digest = layerDigest,
                    },
                },
            }));
        using var content = new ByteArrayContent(manifestBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json");
        var response = await client.PutAsync(
            $"{apiRoot}/{repository}/manifests/{tag}",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return new PushedArtifact(ContentDigest(response), layerDigest);
    }

    private static async Task PushSimpleManifestAsync(
        HttpClient client,
        string apiRoot,
        string repository,
        string tag,
        string marker,
        CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $"{{\"schemaVersion\":2,\"mediaType\":\"application/vnd.oci.image.manifest.v1+json\",\"annotations\":{{\"marker\":\"{marker}\"}},\"layers\":[]}}");
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.oci.image.manifest.v1+json");
        var response = await client.PutAsync(
            $"{apiRoot}/{repository}/manifests/{tag}",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> PushBlobAsync(
        HttpClient client,
        string apiRoot,
        string repository,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var digest = $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
        var start = await client.PostAsync(
            $"{apiRoot}/{repository}/blobs/uploads/",
            content: null,
            cancellationToken);
        start.EnsureSuccessStatusCode();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var complete = await client.PutAsync(
            $"{start.Headers.Location}?digest={Uri.EscapeDataString(digest)}",
            content,
            cancellationToken);
        complete.EnsureSuccessStatusCode();
        return digest;
    }

    private static string ContentDigest(HttpResponseMessage response) =>
        response.Headers.GetValues("Docker-Content-Digest").Single();

    private static async Task AssertRepositoryNotTrackedAsync(
        FeedTestServerHost downstream,
        string repository,
        string? ociSegment,
        CancellationToken cancellationToken)
    {
        await using var scope = downstream.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        Assert.False(await context.OciRepositories.AnyAsync(
            item => item.OciSegment == ociSegment && item.Name == repository,
            cancellationToken));
    }

    private static async Task AssertBlobStoredAsync(
        FeedTestServerHost downstream,
        string digest,
        string? ociSegment,
        bool expected,
        CancellationToken cancellationToken)
    {
        await using var scope = downstream.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IContext>();
        Assert.Equal(expected, await context.OciBlobs.AnyAsync(
            item => item.OciSegment == ociSegment && item.Digest == digest,
            cancellationToken));
    }

    private sealed record PushedArtifact(string ManifestDigest, string LayerDigest);
}

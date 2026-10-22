using System.Net;
using System.Text.Json.Nodes;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Mirror;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AvantiPoint.Packages.Registry.Npm.Tests;

public sealed class NpmMirrorTarballIdentityTests : IClassFixture<NpmTestWebApplicationFactory>
{
    private readonly NpmTestWebApplicationFactory factory;
    public NpmMirrorTarballIdentityTests(NpmTestWebApplicationFactory factory) => this.factory = factory;

    [Theory]
    [InlineData(MirrorCachingStrategy.ProxyOnly, false)]
    [InlineData(MirrorCachingStrategy.IndexAndCache, false)]
    [InlineData(MirrorCachingStrategy.IndexAndCache, true)]
    public async Task SameUpstreamBasename_KeepsEachVersionAndItsBytesDistinct(MirrorCachingStrategy strategy, bool legacyCollision)
    {
        var ct = TestContext.Current.CancellationToken;
        await factory.EnsureDatabaseMigratedAsync();
        var name = $"basename-collision-{Guid.NewGuid():N}";
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<INpmMirrorService>();
            services.AddSingleton<INpmMirrorService>(new CollisionMirror(name, strategy));
        }));
        using var client = host.CreateClient();
        using var response = await client.GetAsync($"/npm/{name}", ct);
        response.EnsureSuccessStatusCode();
        if (legacyCollision)
        {
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IContext>();
            var versions = await db.NpmVersions.Where(v => v.Package.Name == name).ToListAsync(ct);
            foreach (var version in versions) version.TarballPath = $"{name}/-/shared.tgz";
            await db.SaveChangesAsync(ct);
            var store = scope.ServiceProvider.GetRequiredService<IStorageBackendFactory>().CreatePathStore("npm/");
            await store.PutAsync($"{name}/-/shared.tgz", new MemoryStream([99]), ct);
            using var ambiguous = await client.GetAsync($"/npm/{name}/-/shared.tgz", ct);
            Assert.Equal(HttpStatusCode.NotFound, ambiguous.StatusCode);
        }
        var packument = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!;
        var urls = new List<string>();
        foreach (var version in new[] { "1.0.0", "2.0.0" })
        {
            var url = packument["versions"]![version]!["dist"]!["tarball"]!.GetValue<string>();
            urls.Add(url);
            using var download = await client.GetAsync(url, ct);
            Assert.Equal(HttpStatusCode.OK, download.StatusCode);
            Assert.Equal(new byte[] { version[0] == '1' ? (byte)1 : (byte)2 }, await download.Content.ReadAsByteArrayAsync(ct));
            // A second download exercises the cached local path too.
            Assert.Equal(await client.GetByteArrayAsync(url, ct), await download.Content.ReadAsByteArrayAsync(ct));
        }
        Assert.NotEqual(urls[0], urls[1]);
    }

    private sealed class CollisionMirror(string name, MirrorCachingStrategy strategy) : INpmMirrorService
    {
        public MirrorCachingStrategy Strategy => strategy;
        public PackageOrigin MirrorOrigin => PackageOrigin.Mirrored;
        public Task<JsonObject?> FetchPackumentAsync(string packageName, CancellationToken cancellationToken = default)
        {
            var versions = new JsonObject();
            foreach (var version in new[] { "1.0.0", "2.0.0" }) versions[version] = new JsonObject
            {
                ["name"] = name, ["version"] = version,
                ["dist"] = new JsonObject { ["tarball"] = $"https://example.test/{version}/shared.tgz" },
            };
            return Task.FromResult<JsonObject?>(new JsonObject { ["name"] = name, ["versions"] = versions });
        }
        public Task<Stream?> FetchTarballAsync(string tarballUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(new MemoryStream([tarballUrl.Contains("/1.0.0/") ? (byte)1 : (byte)2]));
    }
}

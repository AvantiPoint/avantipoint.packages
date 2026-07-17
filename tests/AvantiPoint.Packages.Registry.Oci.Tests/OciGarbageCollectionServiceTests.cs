using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Metrics;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Database.Sqlite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prometheus;

namespace AvantiPoint.Packages.Registry.Oci.Tests;

public sealed class OciGarbageCollectionServiceTests : IDisposable
{
    private const string ManifestMediaType = "application/vnd.oci.image.manifest.v1+json";
    private const string IndexMediaType = "application/vnd.oci.image.index.v1+json";

    private readonly DateTimeOffset _now = new(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);
    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly MemoryDigestStore _store = new();
    private readonly FeedMetricsService _metrics;

    public OciGarbageCollectionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _metrics = new FeedMetricsService(Metrics.WithCustomRegistry(Metrics.NewCustomRegistry()));
    }

    [Fact]
    public async Task CollectAsync_TraversesManifestGraphAndDeletesOnlyOldOrphans()
    {
        var seeded = await SeedGraphAsync();
        var surface = CreateSurface();
        _metrics.SetBlobBytes(surface, seeded.TotalBytes);
        var collector = CreateCollector();

        var dryRun = await collector.CollectAsync(
            surface,
            dryRun: true,
            minimumAge: TimeSpan.FromHours(24),
            TestContext.Current.CancellationToken);

        Assert.False(dryRun.Deleted);
        Assert.Equal([seeded.OldOrphanDigest], dryRun.Digests);
        Assert.Equal(seeded.OldOrphanSize, dryRun.Bytes);
        Assert.True(_store.Contains(seeded.OldOrphanDigest));

        var deleted = await collector.CollectAsync(
            surface,
            dryRun: false,
            minimumAge: TimeSpan.FromHours(24),
            TestContext.Current.CancellationToken);

        Assert.True(deleted.Deleted);
        Assert.Equal([seeded.OldOrphanDigest], deleted.Digests);
        Assert.False(_store.Contains(seeded.OldOrphanDigest));
        Assert.True(_store.Contains(seeded.RecentOrphanDigest));
        Assert.All(seeded.ReachableDigests, digest => Assert.True(_store.Contains(digest)));
        Assert.False(await _context.OciBlobs.AnyAsync(
            blob => blob.Digest == seeded.OldOrphanDigest,
            TestContext.Current.CancellationToken));
        Assert.Equal(
            seeded.TotalBytes - seeded.OldOrphanSize,
            _metrics.GetBlobBytes()["default:oci"]);
    }

    private OciGarbageCollectionService CreateCollector()
    {
        return new OciGarbageCollectionService(
            _context,
            new StubStorageBackendFactory(_store),
            _metrics,
            new FixedTimeProvider(_now),
            NullLogger<OciGarbageCollectionService>.Instance);
    }

    private async Task<SeededGraph> SeedGraphAsync()
    {
        var config = Encoding.UTF8.GetBytes("{\"architecture\":\"amd64\",\"os\":\"linux\"}");
        var layer = Encoding.UTF8.GetBytes("reachable-layer");
        var configDigest = AddContent(config);
        var layerDigest = AddContent(layer);
        var child = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{ManifestMediaType}}","config":{"digest":"{{configDigest}}"},"layers":[{"digest":"{{layerDigest}}"}]}
            """);
        var childDigest = AddContent(child);
        var root = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{IndexMediaType}}","manifests":[{"digest":"{{childDigest}}"}]}
            """);
        var rootDigest = AddContent(root);
        var oldOrphan = Encoding.UTF8.GetBytes("old-orphan");
        var recentOrphan = Encoding.UTF8.GetBytes("recent-orphan");
        var oldOrphanDigest = AddContent(oldOrphan);
        var recentOrphanDigest = AddContent(recentOrphan);
        var oldCreatedAt = _now.UtcDateTime - TimeSpan.FromDays(2);

        _context.OciBlobs.AddRange(
            Blob(rootDigest, root.Length, oldCreatedAt),
            Blob(childDigest, child.Length, oldCreatedAt),
            Blob(configDigest, config.Length, oldCreatedAt),
            Blob(layerDigest, layer.Length, oldCreatedAt),
            Blob(oldOrphanDigest, oldOrphan.Length, oldCreatedAt),
            Blob(recentOrphanDigest, recentOrphan.Length, _now.UtcDateTime - TimeSpan.FromHours(1)));
        _context.OciManifests.AddRange(
            Manifest(rootDigest, root.Length, IndexMediaType, oldCreatedAt),
            Manifest(childDigest, child.Length, ManifestMediaType, oldCreatedAt));
        _context.OciRepositories.Add(new OciRepository
        {
            Name = "sample",
            Tags =
            [
                new OciTag
                {
                    Tag = "latest",
                    ManifestDigest = rootDigest,
                    Origin = PackageOrigin.Published,
                },
            ],
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededGraph(
            [rootDigest, childDigest, configDigest, layerDigest],
            oldOrphanDigest,
            oldOrphan.Length,
            recentOrphanDigest,
            root.Length + child.Length + config.Length + layer.Length + oldOrphan.Length + recentOrphan.Length);
    }

    private string AddContent(byte[] content)
    {
        var digest = $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";
        _store.Add(digest, content);
        return digest;
    }

    private static OciBlob Blob(string digest, long size, DateTime createdAt) => new()
    {
        Digest = digest,
        Size = size,
        CreatedAt = createdAt,
    };

    private static OciManifest Manifest(
        string digest,
        long size,
        string mediaType,
        DateTime createdAt) => new()
    {
        Digest = digest,
        Size = size,
        MediaType = mediaType,
        CreatedAt = createdAt,
    };

    private static SurfaceContext CreateSurface() => new(
        "default",
        FeedProtocol.Oci,
        "oci",
        null,
        string.Empty,
        new Uri("https://registry.example.test"));

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed record SeededGraph(
        IReadOnlyList<string> ReachableDigests,
        string OldOrphanDigest,
        long OldOrphanSize,
        string RecentOrphanDigest,
        long TotalBytes);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubStorageBackendFactory(IDigestBlobStore store) : IStorageBackendFactory
    {
        public IPathBlobStore CreatePathStore(string subPrefix) => throw new NotSupportedException();

        public IDigestBlobStore CreateDigestStore(string subPrefix) =>
            subPrefix == "oci/"
                ? store
                : throw new InvalidOperationException($"Unexpected storage prefix '{subPrefix}'.");
    }

    private sealed class MemoryDigestStore : IDigestBlobStore
    {
        private readonly Dictionary<string, byte[]> _content = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string digest, byte[] content) => _content[digest] = content;

        public bool Contains(string digest) => _content.ContainsKey(digest);

        public Task PutAsync(
            string algorithm,
            string hex,
            Stream content,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> GetAsync(
            string algorithm,
            string hex,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_content[$"{algorithm}:{hex}"], writable: false));

        public Task<bool> ExistsAsync(
            string algorithm,
            string hex,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_content.ContainsKey($"{algorithm}:{hex}"));

        public Task DeleteAsync(
            string algorithm,
            string hex,
            CancellationToken cancellationToken = default)
        {
            _content.Remove($"{algorithm}:{hex}");
            return Task.CompletedTask;
        }
    }
}

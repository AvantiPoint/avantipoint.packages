using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Core.Entities.Oci;
using AvantiPoint.Packages.Database.Sqlite;
using AvantiPoint.Packages.Host.Admin.Entities;
using AvantiPoint.Packages.Host.Admin.Services.Publishers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Publishers;

public sealed class OciDownstreamPublisherTests : IDisposable
{
    private const string ImageManifestMediaType = "application/vnd.oci.image.manifest.v1+json";
    private const string ImageIndexMediaType = "application/vnd.oci.image.index.v1+json";

    private readonly SqliteConnection _connection;
    private readonly SqliteContext _context;
    private readonly MemoryDigestStore _store = new();
    private readonly ListLogger<OciDownstreamPublisher> _logger = new();

    public OciDownstreamPublisherTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task PushAsync_Image_UploadsMissingBlobsBeforeTaggedManifest()
    {
        var artifact = await SeedImageAsync("sample", "1.0.0");
        var handler = new RecordingRegistryHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/blobs/uploads/"))
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Headers = { Location = new Uri($"/v2/acme/sample/blobs/uploads/{Guid.NewGuid():N}", UriKind.Relative) },
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        var publisher = CreatePublisher(handler);

        var pushed = await publisher.PushAsync(
            new DownstreamPublishRequest("sample", "1.0.0"),
            CreateTarget(),
            TestContext.Current.CancellationToken);

        Assert.True(pushed, _logger.Output);
        Assert.Equal(2, handler.Requests.Count(request =>
            request.Method == HttpMethod.Put && request.Path.Contains("/blobs/uploads/")));

        var manifestRequest = Assert.Single(
            handler.Requests,
            request => request.Method == HttpMethod.Put && request.Path.EndsWith("/manifests/1.0.0"));
        Assert.Equal(artifact.Manifest, manifestRequest.Content);
        Assert.All(
            handler.Requests.Where(request => request.Path.StartsWith("/v2/")),
            request => Assert.StartsWith("/v2/acme/sample/", request.Path));

        var lastBlobUpload = handler.Requests.FindLastIndex(request =>
            request.Method == HttpMethod.Put && request.Path.Contains("/blobs/uploads/"));
        var manifestUpload = handler.Requests.FindIndex(request =>
            request.Method == HttpMethod.Put && request.Path.EndsWith("/manifests/1.0.0"));
        Assert.True(lastBlobUpload < manifestUpload);
    }

    [Fact]
    public async Task PushAsync_Index_PublishesChildManifestBeforeTaggedIndex()
    {
        var child = await SeedImageAsync("sample", "unused-child-tag", addRepositoryTag: false);
        var indexBytes = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{ImageIndexMediaType}}","manifests":[{"mediaType":"{{ImageManifestMediaType}}","size":{{child.Manifest.Length}},"digest":"{{child.ManifestDigest}}"}]}
            """);
        var indexDigest = AddDigest(indexBytes);
        _context.OciManifests.Add(new OciManifest
        {
            Digest = indexDigest,
            MediaType = ImageIndexMediaType,
            ArtifactKind = OciArtifactKind.Index,
            Origin = PackageOrigin.Published,
            Size = indexBytes.Length,
        });
        var repository = await _context.OciRepositories
            .Include(repository => repository.Tags)
            .SingleAsync(TestContext.Current.CancellationToken);
        repository.Tags.Add(new OciTag
        {
            Tag = "latest",
            ManifestDigest = indexDigest,
            Origin = PackageOrigin.Published,
        });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new RecordingRegistryHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Headers = { Location = new Uri($"/v2/acme/sample/blobs/uploads/{Guid.NewGuid():N}", UriKind.Relative) },
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var pushed = await CreatePublisher(handler).PushAsync(
            new DownstreamPublishRequest("sample"),
            CreateTarget(),
            TestContext.Current.CancellationToken);

        Assert.True(pushed, _logger.Output);
        var childUpload = handler.Requests.FindIndex(request =>
            request.Method == HttpMethod.Put
            && request.Path.EndsWith($"/manifests/{Uri.EscapeDataString(child.ManifestDigest)}"));
        var indexUpload = handler.Requests.FindIndex(request =>
            request.Method == HttpMethod.Put && request.Path.EndsWith("/manifests/latest"));
        Assert.True(childUpload >= 0);
        Assert.True(childUpload < indexUpload);
    }

    [Fact]
    public async Task PushAsync_RemoteTagAlreadyMatches_SkipsGraphUpload()
    {
        var artifact = await SeedImageAsync("sample", "latest");
        var handler = new RecordingRegistryHandler(request =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.TryAddWithoutValidation("Docker-Content-Digest", artifact.ManifestDigest);
            return response;
        });

        var pushed = await CreatePublisher(handler).PushAsync(
            new DownstreamPublishRequest("sample", "latest"),
            CreateTarget(),
            TestContext.Current.CancellationToken);

        Assert.True(pushed, _logger.Output);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Head, request.Method);
        Assert.EndsWith("/manifests/latest", request.Path);
        Assert.Contains("application/vnd.oci.image.index.v1+json", request.Accept);
        Assert.Contains("application/vnd.docker.distribution.manifest.list.v2+json", request.Accept);
    }

    [Fact]
    public async Task PushAsync_BearerChallenge_ExchangesBasicCredentials()
    {
        var artifact = await SeedImageAsync("sample", "latest");
        var requestNumber = 0;
        var handler = new RecordingRegistryHandler(request =>
        {
            requestNumber++;
            if (requestNumber == 1)
            {
                Assert.Equal("Basic", request.Authorization?.Scheme);
                var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                response.Headers.WwwAuthenticate.Add(new AuthenticationHeaderValue(
                    "Bearer",
                    "realm=\"https://auth.example.test/token\",service=\"registry.example.test\",scope=\"repository:acme/sample:pull,push\""));
                return response;
            }

            if (request.RequestUri!.Host == "auth.example.test")
            {
                Assert.Equal("Basic", request.Authorization?.Scheme);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"token\":\"exchanged-token\"}", Encoding.UTF8, "application/json"),
                };
            }

            Assert.Equal("Bearer", request.Authorization?.Scheme);
            Assert.Equal("exchanged-token", request.Authorization?.Parameter);
            var success = new HttpResponseMessage(HttpStatusCode.OK);
            success.Headers.TryAddWithoutValidation("Docker-Content-Digest", artifact.ManifestDigest);
            return success;
        });

        var target = CreateTarget();
        target.Username = "publisher";
        target.ApiToken = "password";
        var pushed = await CreatePublisher(handler).PushAsync(
            new DownstreamPublishRequest("sample", "latest"),
            target,
            TestContext.Current.CancellationToken);

        Assert.True(pushed, _logger.Output);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task PushAsync_ExternalBlobUploadLocation_DoesNotReceiveRegistryCredentials()
    {
        await SeedImageAsync("sample", "latest");
        var uploadNumber = 0;
        var handler = new RecordingRegistryHandler(request =>
        {
            if (request.Method == HttpMethod.Head)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Post)
            {
                uploadNumber++;
                return new HttpResponseMessage(HttpStatusCode.Accepted)
                {
                    Headers = { Location = new Uri($"https://storage.example.test/upload/{uploadNumber}") },
                };
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var pushed = await CreatePublisher(handler).PushAsync(
            new DownstreamPublishRequest("sample", "latest"),
            CreateTarget(),
            TestContext.Current.CancellationToken);

        Assert.True(pushed, _logger.Output);
        Assert.All(
            handler.Requests.Where(request => request.RequestUri.Host == "storage.example.test"),
            request => Assert.Null(request.Authorization));
        Assert.All(
            handler.Requests.Where(request => request.RequestUri.Host == "registry.example.test"),
            request => Assert.Equal("Bearer", request.Authorization?.Scheme));
    }

    private OciDownstreamPublisher CreatePublisher(HttpMessageHandler handler)
    {
        var storageFactory = new Mock<IStorageBackendFactory>(MockBehavior.Strict);
        storageFactory.Setup(factory => factory.CreateDigestStore("oci/")).Returns(_store);
        return new OciDownstreamPublisher(
            _context,
            storageFactory.Object,
            new NullSecretProtector(),
            new StubHttpClientFactory(handler),
            _logger);
    }

    private static HostPublishTarget CreateTarget() => new()
    {
        Name = "external-registry",
        Protocol = PublishTargetProtocol.Oci,
        PublishEndpoint = "https://registry.example.test/acme",
        ApiToken = "token",
    };

    private async Task<SeededImage> SeedImageAsync(
        string repositoryName,
        string tag,
        bool addRepositoryTag = true)
    {
        var config = Encoding.UTF8.GetBytes("{\"architecture\":\"amd64\",\"os\":\"linux\"}");
        var layer = Encoding.UTF8.GetBytes("layer-content");
        var configDigest = AddDigest(config);
        var layerDigest = AddDigest(layer);
        var manifest = Encoding.UTF8.GetBytes(
            $$"""
            {"schemaVersion":2,"mediaType":"{{ImageManifestMediaType}}","config":{"mediaType":"application/vnd.oci.image.config.v1+json","size":{{config.Length}},"digest":"{{configDigest}}"},"layers":[{"mediaType":"application/vnd.oci.image.layer.v1.tar","size":{{layer.Length}},"digest":"{{layerDigest}}"}]}
            """);
        var manifestDigest = AddDigest(manifest);

        var repository = await _context.OciRepositories
            .Include(existing => existing.Tags)
            .FirstOrDefaultAsync(existing => existing.Name == repositoryName);
        if (repository is null)
        {
            repository = new OciRepository { Name = repositoryName };
            _context.OciRepositories.Add(repository);
        }

        if (addRepositoryTag)
        {
            repository.Tags.Add(new OciTag
            {
                Tag = tag,
                ManifestDigest = manifestDigest,
                Origin = PackageOrigin.Published,
            });
        }

        _context.OciManifests.Add(new OciManifest
        {
            Digest = manifestDigest,
            MediaType = ImageManifestMediaType,
            ArtifactKind = OciArtifactKind.Image,
            Origin = PackageOrigin.Published,
            Size = manifest.Length,
        });
        _context.OciBlobs.AddRange(
            new OciBlob { Digest = configDigest, Size = config.Length },
            new OciBlob { Digest = layerDigest, Size = layer.Length });
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return new SeededImage(manifest, manifestDigest);
    }

    private string AddDigest(byte[] content)
    {
        var digest = $"sha256:{Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}";
        _store.Add(digest, content);
        return digest;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed record SeededImage(byte[] Manifest, string ManifestDigest);

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri RequestUri,
        AuthenticationHeaderValue? Authorization,
        IReadOnlyList<string> Accept,
        byte[]? Content)
    {
        public string Path => RequestUri.AbsolutePath;
    }

    private sealed class RecordingRegistryHandler(
        Func<CapturedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var captured = new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization,
                request.Headers.Accept.Select(header => header.MediaType!).ToList(),
                request.Content is null
                    ? null
                    : await request.Content.ReadAsByteArrayAsync(cancellationToken));
            Requests.Add(captured);
            return responder(captured);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class MemoryDigestStore : IDigestBlobStore
    {
        private readonly Dictionary<string, byte[]> _content = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string digest, byte[] content) => _content[digest] = content;

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
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        private readonly List<string> _messages = [];

        public string Output => string.Join(Environment.NewLine, _messages);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _messages.Add($"{formatter(state, exception)} {exception}");
    }
}

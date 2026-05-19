using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Elasticsearch;
using AvantiPoint.Packages.Search.Tests.TestInfrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace AvantiPoint.Packages.Search.Tests;

[Collection("SearchIntegration")]
public sealed class ElasticsearchSearchIntegrationTests : IAsyncLifetime
{
    private const string IndexName = "packages-test";
    private IContainer? _container;
    private IOpenSearchClient? _client;
    private ElasticsearchSearchIndexer? _indexer;
    private ElasticsearchSearchService? _search;

    [DockerFact]
    public async Task IndexAndSearch_FindsPackageById()
    {
        Assert.NotNull(_indexer);
        Assert.NotNull(_search);

        var package = new Package
        {
            Id = "Search.Test.Package",
            Listed = true,
            Published = DateTime.UtcNow,
            Description = "integration test",
            Authors = ["test"],
            Tags = ["tag"],
            Version = NuGet.Versioning.NuGetVersion.Parse("1.0.0"),
        };

        await _indexer.IndexAsync(package, CancellationToken.None);
        await _client!.Indices.RefreshAsync(IndexName);

        var response = await _search.SearchAsync(
            new Core.SearchRequest
            {
                Query = null,
                Take = 10,
                Skip = 0,
                IncludePrerelease = false,
                IncludeSemVer2 = false,
            },
            CancellationToken.None);

        Assert.True(response.TotalHits >= 1);
        Assert.Contains(response.Data, p => p.PackageId == "Search.Test.Package");
    }

    public async ValueTask InitializeAsync()
    {
        _container = new ContainerBuilder("opensearchproject/opensearch:2")
            .WithEnvironment("discovery.type", "single-node")
            .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
            .WithEnvironment("OPENSEARCH_JAVA_OPTS", "-Xms512m -Xmx512m")
            .WithPortBinding(9200, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(
                r => r.ForPort(9200).ForPath("/_cluster/health")))
            .Build();

        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(9200);
        var endpoint = $"http://{host}:{port}";

        var options = new ElasticsearchSearchOptions
        {
            Endpoint = endpoint,
            IndexName = IndexName,
            DisableCertificateValidation = true,
        };

        _client = ElasticsearchClientFactory.Create(Options.Create(options));
        _indexer = new ElasticsearchSearchIndexer(
            _client,
            new StubDocumentFactory(),
            Options.Create(options));
        _search = new ElasticsearchSearchService(
            _client,
            new SearchDocumentMapper(new TestUrlGenerator()),
            new FrameworkCompatibilityService(),
            Options.Create(options),
            Options.Create(new SearchOptions()));
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private sealed class StubDocumentFactory : IPackageSearchDocumentFactory
    {
        public Task<PackageSearchDocument?> CreateAsync(string packageId, CancellationToken cancellationToken)
        {
            return Task.FromResult<PackageSearchDocument?>(new PackageSearchDocument
            {
                Key = packageId.ToLowerInvariant(),
                Id = packageId,
                Version = "1.0.0",
                Description = "integration test",
                Authors = ["test"],
                Tags = ["tag"],
                Published = DateTimeOffset.UtcNow,
                VisibilityMask = SearchVisibility.GetProfileBit(includePrerelease: false, includeSemVer2: false),
                Versions = ["1.0.0"],
                VersionDownloads = ["0"],
                VersionIsPrerelease = [false],
                VersionIsSemVer2 = [false],
                Origin = PackageOrigin.Published,
            });
        }
    }
}

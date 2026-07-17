using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace AvantiPoint.Packages.Tests.Search;

public sealed class FederatedSearchServiceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void DependencyInjection_OnlyDecoratesSearchWhenEnabled(
        bool enabled,
        bool expectFederated)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Search:Type"] = "Database",
                ["Search:EnableUpstreamSearch"] = enabled.ToString(),
            })
            .Build();
        var local = new Mock<ISearchService>();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(Mock.Of<IHostEnvironment>(environment =>
            environment.EnvironmentName == Environments.Development));
        services.AddLogging();
        services.AddSingleton(Mock.Of<IMirrorService>());
        services.AddNuGetApiApplication(options =>
            options.Services.AddProvider<ISearchService>((_, _) => local.Object));
        using var provider = services.BuildServiceProvider();

        var search = provider.GetRequiredService<ISearchService>();

        if (expectFederated)
        {
            Assert.IsType<FederatedSearchService>(search);
        }
        else
        {
            Assert.Same(local.Object, search);
        }
    }

    [Theory]
    [InlineData(FederatedSearchMergeStrategy.LocalPreferred, "1.0.0", 3)]
    [InlineData(FederatedSearchMergeStrategy.Deduplicate, "2.0.0", 3)]
    [InlineData(FederatedSearchMergeStrategy.Union, "1.0.0", 4)]
    public async Task SearchAsync_MergesResultsUsingConfiguredStrategy(
        FederatedSearchMergeStrategy strategy,
        string expectedDuplicateVersion,
        int expectedTotalHits)
    {
        SearchRequest? localRequest = null;
        SearchRequest? upstreamRequest = null;
        var local = new Mock<ISearchService>();
        local
            .Setup(service => service.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SearchRequest, CancellationToken>((request, _) => localRequest = request)
            .ReturnsAsync(Response(
                Result("Duplicate", "1.0.0"),
                Result("Local", "1.0.0")));
        var mirror = new Mock<IMirrorService>();
        mirror
            .Setup(service => service.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SearchRequest, CancellationToken>((request, _) => upstreamRequest = request)
            .ReturnsAsync([
                Response(
                    Result("Duplicate", "2.0.0"),
                    Result("Upstream", "1.0.0")),
            ]);
        var service = CreateService(local.Object, mirror.Object, strategy);

        var response = await service.SearchAsync(
            new SearchRequest { Query = "package", Skip = 0, Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedTotalHits, response.TotalHits);
        Assert.Equal(expectedTotalHits, response.Data.Count);
        Assert.Equal(
            expectedDuplicateVersion,
            response.Data.First(result => result.PackageId == "Duplicate").Version);
        Assert.NotNull(localRequest);
        Assert.NotNull(upstreamRequest);
        Assert.Equal(20, localRequest.Take);
        Assert.Equal(20, upstreamRequest.Take);
    }

    [Fact]
    public async Task SearchAsync_AppliesPaginationAfterMerging()
    {
        var local = SearchServiceReturning(Response(
            Result("First", "1.0.0"),
            Result("Second", "1.0.0")));
        var mirror = new Mock<IMirrorService>();
        mirror
            .Setup(service => service.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Response(Result("Third", "1.0.0"))]);
        var service = CreateService(local.Object, mirror.Object, FederatedSearchMergeStrategy.LocalPreferred);

        var response = await service.SearchAsync(
            new SearchRequest { Skip = 1, Take = 1 },
            TestContext.Current.CancellationToken);

        Assert.Equal("Second", Assert.Single(response.Data).PackageId);
        local.Verify(service => service.SearchAsync(
            It.Is<SearchRequest>(request => request.Skip == 0 && request.Take == 2),
            It.IsAny<CancellationToken>()));
        mirror.Verify(service => service.SearchAsync(
            It.Is<SearchRequest>(request => request.Skip == 0 && request.Take == 2),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SearchAsync_WhenUpstreamTimesOut_ReturnsLocalResults()
    {
        var local = SearchServiceReturning(Response(Result("Local", "1.0.0")));
        var mirror = new Mock<IMirrorService>();
        mirror
            .Setup(service => service.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .Returns<SearchRequest, CancellationToken>(async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return [];
            });
        var service = CreateService(
            local.Object,
            mirror.Object,
            FederatedSearchMergeStrategy.LocalPreferred,
            TimeSpan.FromMilliseconds(25));

        var response = await service.SearchAsync(
            new SearchRequest { Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal("Local", Assert.Single(response.Data).PackageId);
        Assert.Equal(1, response.TotalHits);
    }

    [Fact]
    public async Task SearchAsync_WhenUpstreamFails_ReturnsLocalResults()
    {
        var local = SearchServiceReturning(Response(Result("Local", "1.0.0")));
        var mirror = new Mock<IMirrorService>();
        mirror
            .Setup(service => service.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("unavailable"));
        var service = CreateService(
            local.Object,
            mirror.Object,
            FederatedSearchMergeStrategy.LocalPreferred);

        var response = await service.SearchAsync(
            new SearchRequest { Take = 20 },
            TestContext.Current.CancellationToken);

        Assert.Equal("Local", Assert.Single(response.Data).PackageId);
        Assert.Equal(1, response.TotalHits);
    }

    private static FederatedSearchService CreateService(
        ISearchService local,
        IMirrorService mirror,
        FederatedSearchMergeStrategy strategy,
        TimeSpan? timeout = null) =>
        new(
            local,
            mirror,
            Options.Create(new SearchOptions
            {
                EnableUpstreamSearch = true,
                MergeStrategy = strategy,
                UpstreamSearchTimeout = timeout ?? TimeSpan.FromSeconds(1),
            }),
            NullLogger<FederatedSearchService>.Instance);

    private static Mock<ISearchService> SearchServiceReturning(SearchResponse response)
    {
        var service = new Mock<ISearchService>();
        service
            .Setup(search => search.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
        return service;
    }

    private static SearchResponse Response(params SearchResult[] results) => new()
    {
        Context = new SearchContext(),
        TotalHits = results.Length,
        Data = results,
    };

    private static SearchResult Result(string id, string version) => new()
    {
        PackageId = id,
        Version = version,
        Versions = [],
    };
}

using System.Net.Http.Json;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Integration.Tests.TestInfrastructure;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.Integration.Tests;

public sealed class FederatedSearchIntegrationTests
{
    [Fact]
    public async Task Search_FindsUpstreamPackageWithoutMirroringItLocally()
    {
        var packageId = $"Federated.Search.{Guid.NewGuid():N}";
        await using var upstream = await UpstreamOpenFeedHost.StartAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await upstream.SeedPackageAsync(
            packageId,
            "1.0.0",
            TestContext.Current.CancellationToken);
        await using var feed = await FeedUnderTestHost.StartAsync(
            new FeedUnderTestOptions
            {
                UpstreamServiceIndexUrl = upstream.ServiceIndexUrl,
                CachingStrategy = PackageSourceCachingStrategy.ProxyOnly,
                IncludeMirroredPackages = false,
                EnableUpstreamSearch = true,
                MergeStrategy = FederatedSearchMergeStrategy.LocalPreferred,
            },
            TestContext.Current.CancellationToken);

        var response = await feed.Client.GetFromJsonAsync<SearchResponse>(
            $"/v3/search?q={Uri.EscapeDataString(packageId)}&take=20&semVerLevel=2.0.0",
            TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Contains(
            response.Data,
            result => string.Equals(result.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
        Assert.Null(await feed.FindPackageAsync(
            packageId,
            "1.0.0",
            TestContext.Current.CancellationToken));
        Assert.Equal(0, FeedUnderTestHost.CountPackageFiles(feed.StoragePath));
    }
}

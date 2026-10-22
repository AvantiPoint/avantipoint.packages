using AvantiPoint.Feed.Platform.Storage;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Host.Admin.Extensions;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace AvantiPoint.Packages.Host.Admin.Tests.Services;

public sealed class HostDependencyHealthCheckTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Storage_ValidatesRoundtripAndCleansUp(bool corrupt)
    {
        byte[] bytes = [];
        var store = new Mock<IPathBlobStore>();
        store.Setup(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, Stream input, CancellationToken cancellationToken) =>
            {
                using var output = new MemoryStream();
                await input.CopyToAsync(output, cancellationToken);
                bytes = output.ToArray();
            });
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream(corrupt ? new byte[bytes.Length] : bytes));
        var factory = new Mock<IStorageBackendFactory>();
        factory.Setup(f => f.CreatePathStore("_health")).Returns(store.Object);
        using var provider = new ServiceCollection().AddSingleton(factory.Object).BuildServiceProvider();
        var check = new HostStorageHealthCheck(provider.GetRequiredService<IServiceScopeFactory>());
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => check.CheckHealthAsync(new(), TestContext.Current.CancellationToken)));
        Assert.All(results, result => Assert.Equal(corrupt ? HealthStatus.Unhealthy : HealthStatus.Healthy, result.Status));
        store.Verify(s => s.PutAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Search_UsesBoundedLocalQueryAndReportsFailure()
    {
        var search = new Mock<ISearchService>();
        search.Setup(s => s.AutocompleteAsync(It.Is<AutocompleteRequest>(r => r.Take == 1), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("test search unavailable"));
        using var provider = new ServiceCollection().AddSingleton(search.Object).BuildServiceProvider();
        var check = new HostSearchHealthCheck(provider.GetRequiredService<IServiceScopeFactory>());
        var result = await check.CheckHealthAsync(new(), TestContext.Current.CancellationToken);
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        search.Verify(s => s.SearchAsync(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

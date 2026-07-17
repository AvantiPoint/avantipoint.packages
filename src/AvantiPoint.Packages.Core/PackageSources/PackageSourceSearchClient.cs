using System;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.Core;

internal sealed class PackageSourceSearchClient(ISecretProtector secretProtector) : IPackageSourceSearchClient
{
    public async Task<SearchResponse> SearchAsync(
        PackageSource source,
        SearchRequest request,
        CancellationToken cancellationToken)
    {
        using var httpClient = PackageSourceHttpClientFactory.Create(
            source,
            Timeout.InfiniteTimeSpan,
            secretProtector);
        var client = new NuGetClientFactory(httpClient, source.FeedUrl).CreateSearchClient();

        return await client.SearchAsync(
            request.Query,
            request.Skip,
            request.Take,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            request.Framework,
            cancellationToken);
    }
}

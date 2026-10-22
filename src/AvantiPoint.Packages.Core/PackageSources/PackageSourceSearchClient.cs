using System;
using System.Linq;
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

        var frameworks = FrameworkSearchFilter.GetRequestedFrameworks(request);
        return await client.SearchAsync(
            request.Query,
            request.Skip,
            request.Take,
            request.IncludePrerelease,
            request.IncludeSemVer2,
            request.PackageType,
            request.Framework ?? frameworks.FirstOrDefault(),
            cancellationToken,
            frameworks,
            request.IncludeCompatibleFrameworks,
            request.FrameworkFilterMode.ToString());
    }
}

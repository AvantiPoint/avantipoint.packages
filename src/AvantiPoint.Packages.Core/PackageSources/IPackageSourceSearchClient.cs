using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Protocol.Models;

namespace AvantiPoint.Packages.Core;

internal interface IPackageSourceSearchClient
{
    Task<SearchResponse> SearchAsync(
        PackageSource source,
        SearchRequest request,
        CancellationToken cancellationToken);
}

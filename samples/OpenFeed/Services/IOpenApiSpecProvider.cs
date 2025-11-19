using System.Threading;
using System.Threading.Tasks;
using Microsoft.OpenApi;

namespace OpenFeed.Services;

public interface IOpenApiSpecProvider
{
    Task<OpenApiDocument?> GetAsync(CancellationToken cancellationToken = default);
}

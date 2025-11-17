using System.Threading;
using System.Threading.Tasks;

namespace OpenFeed.Services;

public interface IOpenApiSpecProvider
{
    Task<OpenApiDocument?> GetAsync(CancellationToken cancellationToken = default);
}

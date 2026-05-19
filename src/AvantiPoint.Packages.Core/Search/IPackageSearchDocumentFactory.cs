using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core;

public interface IPackageSearchDocumentFactory
{
    Task<PackageSearchDocument?> CreateAsync(string packageId, CancellationToken cancellationToken);
}

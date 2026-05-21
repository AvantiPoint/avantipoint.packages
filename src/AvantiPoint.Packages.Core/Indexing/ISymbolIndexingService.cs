using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The result of attempting to index a symbol package.
    /// See <see cref="ISymbolIndexingService.IndexAsync(Stream, CancellationToken)"/>.
    /// </summary>
    /// <summary>
    /// The service used to accept new symbol packages.
    /// </summary>
    public interface ISymbolIndexingService
    {
        /// <summary>
        /// Attempt to index a new symbol package.
        /// </summary>
        /// <param name="stream">The stream containing the symbol package's content.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The result of the attempted indexing operation.</returns>
        Task<SymbolIndexingResult> IndexAsync(Stream stream, CancellationToken cancellationToken);
    }
}

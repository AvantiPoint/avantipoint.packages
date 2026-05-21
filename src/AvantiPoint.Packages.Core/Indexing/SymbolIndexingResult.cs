using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace AvantiPoint.Packages.Core
{
        public record SymbolIndexingResult
        {
            public string PackageId { get; init; }
            public string PackageVersion { get; init; }
            public SymbolIndexingStatus Status { get; init; }
        }
}

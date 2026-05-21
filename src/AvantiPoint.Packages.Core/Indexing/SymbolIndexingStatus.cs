using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace AvantiPoint.Packages.Core
{
        public enum SymbolIndexingStatus
        {
            /// <summary>
            /// The symbol package is malformed.
            /// </summary>
            InvalidSymbolPackage,

            /// <summary>
            /// A corresponding package with the provided ID and version does not exist.
            /// </summary>
            PackageNotFound,

            /// <summary>
            /// The symbol package has been indexed successfully.
            /// </summary>
            Success,
        }
}

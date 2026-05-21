#nullable enable
namespace AvantiPoint.Packages.Core
{
        public enum PackageIndexingStatus
        {
            /// <summary>
            /// The package is malformed. This may also happen if AvantiPoint Packages is in a corrupted state.
            /// </summary>
            InvalidPackage,

            /// <summary>
            /// The package has already been indexed.
            /// </summary>
            PackageAlreadyExists,

            /// <summary>
            /// The package has been indexed successfully.
            /// </summary>
            Success,
        }
}

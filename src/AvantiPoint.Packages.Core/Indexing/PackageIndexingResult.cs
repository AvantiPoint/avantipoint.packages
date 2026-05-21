#nullable enable
namespace AvantiPoint.Packages.Core
{
        public record PackageIndexingResult
        {
            public string? PackageId { get; init; }
            public string? PackageVersion { get; init; }
            public PackageIndexingStatus Status { get; init; }
        }
}

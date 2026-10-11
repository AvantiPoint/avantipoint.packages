using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.UI.Services;

public sealed record NativePackageVersion(string Version, IReadOnlyList<NativeArtifact> Artifacts);

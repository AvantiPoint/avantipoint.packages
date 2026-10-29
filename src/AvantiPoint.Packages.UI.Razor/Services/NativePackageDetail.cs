namespace AvantiPoint.Packages.UI.Services;

public sealed record NativePackageDetail(string Name, IReadOnlyList<NativePackageVersion> Versions);

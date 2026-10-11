namespace AvantiPoint.Packages.UI.Services;

public sealed record NativePackagePage(IReadOnlyList<NativePackageDetail> Packages, int Page, bool HasNextPage);

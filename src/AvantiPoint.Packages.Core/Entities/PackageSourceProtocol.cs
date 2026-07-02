namespace AvantiPoint.Packages.Core;

/// <summary>
/// The registry protocol a <see cref="PackageSource"/> speaks. Existing rows default to
/// <see cref="NuGet"/>.
/// </summary>
public enum PackageSourceProtocol
{
    NuGet = 0,
    Npm = 1,
    Oci = 2
}

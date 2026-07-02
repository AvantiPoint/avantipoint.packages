namespace AvantiPoint.Packages.Host.Admin.Entities;

/// <summary>
/// The registry protocol a <see cref="HostPublishTarget"/> speaks. Existing rows default
/// to <see cref="NuGet"/>.
/// </summary>
public enum PublishTargetProtocol
{
    NuGet = 0,
    Npm = 1,
    Oci = 2
}

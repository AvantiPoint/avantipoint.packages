using AvantiPoint.Packages.Protocol;

namespace AvantiPoint.Packages.Core
{
    public interface IUpstreamNuGetSource
    {
        string Name { get; }
        NuGetClient Client { get; }
    }
}

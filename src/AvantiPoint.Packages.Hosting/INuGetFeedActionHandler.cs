using System.Threading.Tasks;

namespace AvantiPoint.Packages.Hosting
{
    public interface INuGetFeedActionHandler
    {
        Task OnPackageDownloaded(string packageId, string version);
        Task OnPackageUploaded(string packageId, string version);
        Task OnSymbolsUploaded(string packageId, string version);
    }
}

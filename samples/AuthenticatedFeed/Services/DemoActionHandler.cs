using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;

namespace AuthenticatedFeed.Services
{
    public class DemoActionHandler : INuGetFeedActionHandler
    {
        public Task OnPackageDownloaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            return Task.CompletedTask;
        }

        public Task OnPackageUploaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            return Task.CompletedTask;
        }

        public Task OnSymbolsUploaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            return Task.CompletedTask;
        }
    }
}

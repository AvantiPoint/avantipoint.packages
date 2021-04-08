using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Http;

namespace AuthenticatedFeed.Services
{
    public class DemoActionHandler : INuGetFeedActionHandler
    {
        private HttpContext HttpContext { get; }

        public DemoActionHandler(IHttpContextAccessor httpContextAccessor)
        {
            HttpContext = httpContextAccessor.HttpContext;
        }

        public Task<bool> CanDownloadPackage(string packageId, string version)
        {
            // Validates that the user has rights to download the specified package id
            return Task.FromResult(true);
        }

        public Task OnPackageDownloaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            (var username, var _) = GetUserCredentials();
            // Collect Download metrics specific to the currently authenticated user
            return Task.CompletedTask;
        }

        public Task OnSymbolsDownloaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            (var username, var _) = GetUserCredentials();
            // Collect Download metrics specific to the currently authenticated user
            return Task.CompletedTask;
        }

        public Task OnPackageUploaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            return EmailUser("packageUploaded", packageId, version);
        }

        public Task OnSymbolsUploaded(string packageId, string version)
        {
            System.Diagnostics.Debugger.Break();
            return EmailUser("symbolsUploaded", packageId, version);
        }

        private Task EmailUser(string emailTemplate, string packageId, string version)
        {
            (var username, var email) = GetUserCredentials();
            return Task.CompletedTask;
        }

        private (string username, string email) GetUserCredentials()
        {
            var user = HttpContext.User;
            var username = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name).Value;
            var email = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Email).Value;

            return (username, email);
        }
    }
}

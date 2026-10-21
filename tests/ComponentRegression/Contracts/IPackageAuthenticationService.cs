using System.Threading;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core
{
    public interface IPackageAuthenticationService
    {
        Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);

        Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken);
    }
}

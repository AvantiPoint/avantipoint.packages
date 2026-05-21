using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Services;

public interface IHostUserProvisioner
{
    Task<HostUser> ProvisionUserAsync(ClaimsPrincipal principal, HostExternalAuthProvider provider, CancellationToken cancellationToken = default);
}

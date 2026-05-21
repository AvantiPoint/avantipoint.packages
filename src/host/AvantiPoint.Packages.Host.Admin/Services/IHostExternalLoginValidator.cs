using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Entities;

namespace AvantiPoint.Packages.Host.Admin.Services;

public interface IHostExternalLoginValidator
{
    Task<HostLoginValidationResult> ValidateAsync(
        ClaimsPrincipal principal,
        HostExternalAuthProvider provider,
        string? oauthAccessToken = null,
        CancellationToken cancellationToken = default);
}

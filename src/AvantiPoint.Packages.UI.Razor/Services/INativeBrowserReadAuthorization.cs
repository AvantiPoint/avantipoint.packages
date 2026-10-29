using System.Security.Claims;

namespace AvantiPoint.Packages.UI.Services;

/// <summary>Revalidates a host's current permissions for an authenticated browser session.</summary>
public interface INativeBrowserReadAuthorization
{
    Task<bool> CanReadAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

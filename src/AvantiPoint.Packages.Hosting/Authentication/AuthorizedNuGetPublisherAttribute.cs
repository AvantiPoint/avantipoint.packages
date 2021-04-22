using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    public class AuthorizedNuGetPublisherAttribute : AuthorizedNuGetActionFilterAttribute
    {
        public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptionsSnapshot<PackageFeedOptions>>();
            if (options.Value.IsReadOnlyMode)
            {
                context.HttpContext.Response.StatusCode = 401;
                return Task.CompletedTask;
            }

            return base.OnActionExecutionAsync(context, next);
        }

        protected override Task<NuGetAuthenticationResult> IsAuthorized(IPackageAuthenticationService authenticationService)
        {
            GetApiToken(out var apiKey);
            return authenticationService.AuthenticateAsync(apiKey, default);
        }
    }
}

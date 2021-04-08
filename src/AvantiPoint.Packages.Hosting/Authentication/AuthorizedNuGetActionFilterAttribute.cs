using System;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    public abstract class AuthorizedNuGetActionFilterAttribute : ActionFilterAttribute
    {
        public const string ApiKeyHeader = "X-NuGet-ApiKey";

        private HttpContext HttpContext;

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            HttpContext = context.HttpContext;
            var authService = context.HttpContext.RequestServices.GetService<IPackageAuthenticationService>();
            if(authService is not null)
            {
                var result = await IsAuthorized(authService);
                if(!result.Succeeded)
                {
                    context.Result = result.CreateActionResult();
                    return;
                }

                if(result.User is not null)
                    HttpContext.User = result.User;
            }

            await base.OnActionExecutionAsync(context, next);
        }

        protected abstract Task<NuGetAuthenticationResult> IsAuthorized(IPackageAuthenticationService authenticationService);

        protected void GetApiToken(out string apiKey)
        {
            try
            {
                apiKey = HttpContext.Request.Headers[ApiKeyHeader];
            }
            catch
            {
                apiKey = null;
            }
        }

        protected void GetUserCredentials(out string username, out string password)
        {
            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(HttpContext.Request.Headers["Authorization"]);
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
                username = credentials[0];
                password = credentials[1];
            }
            catch
            {
                username = null;
                password = null;
            }
        }
    }
}

using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    public abstract class AuthorizedNuGetActionFilterAttribute : ActionFilterAttribute
    {
        public const string ApiKeyHeader = "X-NuGet-ApiKey";

        private HttpContext HttpContext;

        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            HttpContext = context.HttpContext;
            try
            {
                var authService = context.HttpContext.RequestServices.GetService<IPackageAuthenticationService>();
                if (authService is not null)
                {
                    var result = await IsAuthorized(authService);
                    if (!result.Succeeded)
                    {
                        SetFailedResponse(context, result);
                        await context.HttpContext.Response.CompleteAsync();
                        return;
                    }

                    if (result.User is not null)
                        HttpContext.User = result.User;
                }

                await base.OnActionExecutionAsync(context, next);
            }
            catch (Exception ex)
            {
                var loggerFactory = context.HttpContext.RequestServices.GetService<ILoggerFactory>();
                if (loggerFactory is not null)
                {
                    var logger = loggerFactory.CreateLogger(GetType().Name.Replace("Attribute", string.Empty));
                    logger.LogError(ex, "An unexpected error occurred while processing the user authentication.");
                }
                context.HttpContext.Response.StatusCode = 404;
                await context.HttpContext.Response.CompleteAsync();
                return;
            }
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

        private void SetFailedResponse(ActionExecutingContext context, NuGetAuthenticationResult result)
        {
            if (!string.IsNullOrEmpty(result.Realm))
                context.HttpContext.Response.Headers.Add("Www-Authenticate", GetRealm(result.Realm));

            context.HttpContext.Response.Headers.Add("X-Frame-Options", "Deny");
            context.HttpContext.Response.Headers.Add("X-Nuget-Warning", result.Message);
            context.HttpContext.Response.Headers.Add("Server", result.Server);
            context.HttpContext.Response.StatusCode = 401;
            
        }

        private string GetRealm(string realm)
        {
            if (realm.StartsWith("Basic realm =\""))
                return realm;
            else if (realm.Contains('"'))
            {
                var i = realm.IndexOf('"');
                realm = Regex.Replace(realm.Substring(i), "\"", string.Empty);
            }
            return $"Basic realm =\"{realm}\"";
        }
    }
}

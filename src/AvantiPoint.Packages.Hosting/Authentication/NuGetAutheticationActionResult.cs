using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    internal class NuGetAutheticationActionResult : IActionResult
    {
        private NuGetAuthenticationResult _result { get; }

        public NuGetAutheticationActionResult(NuGetAuthenticationResult result)
        {
            _result = result;
        }

        public Task ExecuteResultAsync(ActionContext context)
        {
            if (!string.IsNullOrEmpty(_result.Realm))
                context.HttpContext.Response.Headers.Add("Www-Authenticate", GetRealm(_result.Realm));

            context.HttpContext.Response.Headers.Add("X-Frame-Options", "Deny");
            context.HttpContext.Response.Headers.Add("X-Nuget-Warning", _result.Message);
            context.HttpContext.Response.Headers.Add("Server", _result.Server);
            context.HttpContext.Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        private string GetRealm(string realm)
        {
            if (realm.StartsWith("Basic realm =\""))
                return realm;
            else if(realm.Contains('"'))
            {
                var i = realm.IndexOf('"');
                realm = Regex.Replace(realm.Substring(i), "\"", string.Empty);
            }
            return $"Basic realm =\"{realm}\"";
        }
    }
}

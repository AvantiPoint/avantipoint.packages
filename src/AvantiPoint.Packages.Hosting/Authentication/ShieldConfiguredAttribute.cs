using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AvantiPoint.Packages.Hosting.Authentication
{
    internal class ShieldConfiguredAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<PackageFeedOptions>>();
            if(string.IsNullOrEmpty(options.Value.Shield?.ServerName))
            {
                context.HttpContext.Response.StatusCode = 404;
                await context.HttpContext.Response.CompleteAsync();
                return;
            }

            await base.OnActionExecutionAsync(context, next);
        }
    }
}

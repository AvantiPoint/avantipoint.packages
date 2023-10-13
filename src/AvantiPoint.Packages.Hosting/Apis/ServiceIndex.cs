using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AvantiPoint.Packages.Hosting;

#nullable enable
internal static class ServiceIndex
{
    public static WebApplication MapServiceIndex(this WebApplication app)
    {
        app.MapGet("v3/index.json", GetServiceIndex)
           .AllowAnonymous()
           .WithTags(nameof(ServiceIndex))
           .WithName(Routes.IndexRouteName);
        return app;
    }

    [ProducesResponseType(typeof(ServiceIndexResponse), 200, "application/json")]
    private static async Task<IResult> GetServiceIndex(IServiceIndexService indexService, CancellationToken cancellationToken)
    {
        return Results.Ok(await indexService.GetAsync(cancellationToken));
    }
}

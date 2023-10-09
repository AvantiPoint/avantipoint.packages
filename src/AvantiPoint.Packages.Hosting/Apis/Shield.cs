using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NGraphics;

namespace AvantiPoint.Packages.Hosting;

internal static class Shield
{
    public static WebApplication MapShieldRoutes(this WebApplication app) => 
        app.AddShields()
        ? app.MapStable()
             .MapLatest()
        : app;

    private static WebApplication MapStable(this WebApplication app)
    {
        app.MapGet("shield/{packageId}", GetStable)
           .AllowAnonymous()
           .WithTags(nameof(Shield))
           .WithName(nameof(GetStable));

        return app;
    }

    private static async ValueTask<IResult> GetStable(string packageId, IContext context, IOptions<PackageFeedOptions> options)
    {
        var packages = await context.Packages
                .AsQueryable()
                .Where(x => x.Id.ToLower() == packageId.ToLower()
                            && x.IsPrerelease == false)
                .ToListAsync();

        var package = packages.OrderByDescending(x => x.Version)
            .FirstOrDefault();

        return GetShield(package, options.Value);
    }

    private static WebApplication MapLatest(this WebApplication app)
    {
        app.MapGet("shield/{packageId}/vpre", GetLatest)
           .AllowAnonymous()
           .WithTags(nameof(Shield))
           .WithName(nameof(GetLatest));
        return app;
    }

    private static async ValueTask<IResult> GetLatest(string packageId, IContext context, IOptions<PackageFeedOptions> options)
    {
        var packages = await context.Packages
                .AsQueryable()
                .Where(x => x.Id.ToLower() == packageId.ToLower())
                .ToListAsync();

        var package = packages.OrderByDescending(x => x.Version)
            .FirstOrDefault();

        return GetShield(package, options.Value);
    }

    private static IResult GetShield(Package package, PackageFeedOptions options)
    {
        var content = DrawShield(package, options);
        //Response.Headers.Add("Cache-Control", "max-age=3600");
        return Results.Content(content, "image/svg+xml");
    }

    private static string DrawShield(Package package, PackageFeedOptions options)
    {
        var k = options.Shield.ServerName;
        var v = package is null ? "Package not found" : $"v{package.Version.OriginalVersion}";

        var font = new Font("DejaVu Sans,Verdana,Geneva,sans-serif", 11);
        var kw = (int)Math.Round(NullPlatform.GlobalMeasureText(k, font).Width * 1.15);
        var vw = (int)Math.Round(NullPlatform.GlobalMeasureText(v, font).Width * 1.15);

        var hpad = 8;
        var w = kw + vw + 4 * hpad;
        var h = 20;
        var c = new GraphicCanvas(new Size(w, h));

        var badgeColor = package?.IsPrerelease switch
        {
            null => "#FFA500",
            true => "#BBB90F",
            false => "#00008B"
        };

        c.FillRectangle(new Rect(0, 0, w, h), new Size(3, 3), "#555");
        c.FillRectangle(new Rect(kw + 2 * hpad, 0, w - 2 * hpad - kw, h), new Size(3, 3), badgeColor);
        c.FillRectangle(new Rect(kw + 2 * hpad, 0, 6, h), badgeColor);

        var scolor = new Color(1.0 / 255.0, 0.3);
        // c.FillRectangle(new Rect(hpad, 5, kw, font.Size), "#F00");
        c.DrawText(k, new Point(hpad, 15), font, scolor);
        c.DrawText(k, new Point(hpad, 14), font, Colors.White);

        // c.FillRectangle(new Rect(w - vw - hpad, 5, vw, font.Size), "#FF0");
        c.DrawText(v, new Point(w - vw - hpad, 15), font, scolor);
        c.DrawText(v, new Point(w - vw - hpad, 14), font, Colors.White);

        using var tw = new StringWriter();
        c.Graphic.WriteSvg(tw);
        return tw.ToString();
    }

    private static bool AddShields(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<PackageFeedOptions>>();
        return !string.IsNullOrEmpty(options.Value.Shield?.ServerName);
    }
}

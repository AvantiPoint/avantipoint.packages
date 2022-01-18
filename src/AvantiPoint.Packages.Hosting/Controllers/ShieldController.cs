using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NGraphics;

namespace AvantiPoint.Packages.Hosting.Controllers
{
    [AllowAnonymous]
    public class ShieldController : ControllerBase
    {
        private IContext _context { get; }
        private PackageFeedOptions _options { get; }

        public ShieldController(IContext context, IOptions<PackageFeedOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        [ShieldConfigured]
        public async Task<IActionResult> GetStable(string packageId)
        {
            var packages = await _context.Packages
                .AsQueryable()
                .Where(x => x.Id.ToLower() == packageId.ToLower()
                            && x.IsPrerelease == false)
                .ToListAsync();

            var package = packages.OrderByDescending(x => x.Version)
                .FirstOrDefault();

            return Shield(package);
        }

        [ShieldConfigured]
        public async Task<IActionResult> GetLatest(string packageId)
        {
            var packages = await _context.Packages
                .AsQueryable()
                .Where(x => x.Id.ToLower() == packageId.ToLower())
                .ToListAsync();

            var package = packages.OrderByDescending(x => x.Version)
                .FirstOrDefault();

            return Shield(package);
        }

        private IActionResult Shield(Package package)
        {
            var content = DrawShield(package);
            Response.Headers.Add("Cache-Control", "max-age=3600");
            var result = Content(content);
            result.ContentType = "image/svg+xml";
            return result;
        }

        private string DrawShield(Package package)
        {
            var k = _options.Shield.ServerName;
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
    }
}

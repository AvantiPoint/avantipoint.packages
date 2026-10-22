using System.Security.Claims;
using AvantiPoint.Packages.Host.Admin.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using AvantiPoint.Packages.Host.Admin.Data;
using AvantiPoint.Packages.Host.Admin.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace AvantiPoint.Packages.UI.Tests;

internal sealed class CaptureCookieStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.UseAuthentication();
        app.Map("/__capture/login", branch => branch.Run(async http =>
        {
            var role = http.Request.Query["role"].ToString();
            var db = http.RequestServices.GetRequiredService<IHostIdentityContext>();
            var user = await db.HostUsers.FirstOrDefaultAsync(user => user.Email == "synthetic@example.test");
            if (user is null)
            {
                user = new HostUser { Email = "synthetic@example.test", Name = "Synthetic administrator" };
                db.HostUsers.Add(user);
            }
            user.IsAdmin = role == "" || role == "admin";
            user.CanConsume = role != "publisher";
            user.CanPublish = role == "publisher";
            user.IsRevoked = false;
            user.ApprovalStatus = HostUserApprovalStatus.Approved;
            await db.SaveChangesAsync();
            var claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, "synthetic-admin"),
                new Claim(ClaimTypes.Name, "Synthetic administrator"),
                new Claim(ClaimTypes.Email, "synthetic@example.test"),
            };
            if (role != "publisher") claims.Add(new Claim(ClaimTypes.Role, FeedRoles.Consumer));
            if (role == "publisher") claims.Add(new Claim(ClaimTypes.Role, FeedRoles.Publisher));
            if (role == "" || role == "admin") claims.Add(new Claim(ClaimTypes.Role, FeedRoles.Admin));
            var identity = new ClaimsIdentity(claims, "Cookies");
            await http.SignInAsync("Cookies", new ClaimsPrincipal(identity));
            http.Response.Redirect("/native/maven");
        }));
        next(app);
    };
}

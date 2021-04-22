using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Core;

namespace OpenFeed
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddNuGetPackagApi(app =>
            {
                app.AddFileStorage()
                   .AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json")
                   .AddSqliteDatabase()
                   .AddSqlServerDatabase();
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
#if DEBUG
                using var scope = app.ApplicationServices.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<SqlServerContext>();
                db.Database.EnsureCreated();
#endif
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseOperationCancelledMiddleware();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapNuGetApiRoutes();
            });
        }
    }
}

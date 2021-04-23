using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuthenticatedFeed.Services;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.Hosting.Authentication;
using AvantiPoint.Packages.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuthenticatedFeed
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IPackageAuthenticationService, DemoNuGetAuthenticationService>();
            services.AddScoped<INuGetFeedActionHandler, DemoActionHandler>();

            services.AddNuGetPackagApi(app =>
            {
                app.AddFileStorage()
                   .AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json")
                   .AddSqlServerDatabase(x => 
                        x.ConnectionString = Configuration.GetConnectionString("DefaultConnection"));
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

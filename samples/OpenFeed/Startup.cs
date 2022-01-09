using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Database.SqlServer;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenFeed
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddNuGetPackagApi(options =>
            {
                options.AddFileStorage();
                //.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json")

                switch (options.EnvironmentName)
                {
                    case "SqlServer":
                        options.AddSqlServerDatabase("SqlServer");
                        break;
                    case "MariaDb":
                        options.AddMariaDb("MariaDb");
                        break;
                    case "MySql":
                        options.AddMySqlDatabase("MySql");
                        break;
                    default:
                        options.AddSqliteDatabase("Sqlite");
                        break;
                }
            });
        }

        public static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
#if DEBUG
                using var scope = app.ApplicationServices.CreateScope();
                using var db = scope.ServiceProvider.GetRequiredService<IContext>();
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

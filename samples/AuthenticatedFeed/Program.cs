using AuthenticatedFeed.Services;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleDataGenerator;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IPackageAuthenticationService, DemoNuGetAuthenticationService>()
    .AddScoped<INuGetFeedActionHandler, DemoActionHandler>()
    .AddNuGetPackageApi(app =>
    {
        app.AddFileStorage()
           //.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json")
           .AddSqlServerDatabase("DefaultConnection");
    });

// Add sample data seeder to populate feed with packages from NuGet.org
builder.Services.AddSampleDataSeeder();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
#if DEBUG
    using var scope = app.Services.CreateScope();
    using var db = scope.ServiceProvider.GetRequiredService<IContext>();
    db.Database.EnsureCreated();
#endif
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();

app.MapNuGetApiRoutes();

await app.RunAsync();

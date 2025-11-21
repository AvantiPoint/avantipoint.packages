using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.UI;
using OpenFeed.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SampleDataGenerator;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    //.AddUpstreamSource("NuGet.org", "https://api.nuget.org/v3/index.json")

    switch (options.Options.Database.Type)
    {
        case "SqlServer":
            options.AddSqlServerDatabase("SqlServer");
            break;
        //case "MariaDb":
        //    options.AddMariaDb("MariaDb");
        //    break;
        //case "MySql":
        //    options.AddMySqlDatabase("MySql");
        //    break;
        default:
            options.AddSqliteDatabase("Sqlite");
            break;
    }
});
builder.Services.AddNuGetApiDocumentation();

// Add Blazor and package search UI
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure the search service to auto-discover endpoints from the local feed
// By default, it will use the current host's /v3/index.json endpoint
builder.Services.AddNuGetSearchService();

// OpenAPI spec provider for dynamic API docs
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IOpenApiSpecProvider, OpenApiSpecProvider>();

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

// Serve static web assets (Blazor framework files, scoped CSS, RCL content)
app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAntiforgery();

app.MapOpenApi();

app.MapNuGetApiRoutes();

// Map Blazor components
app.MapRazorComponents<OpenFeed.Components.App>()
    .AddInteractiveServerRenderMode();

// Explicitly map static assets (required in some hosting scenarios for interactive components)
app.MapStaticAssets();

await app.RunAsync();

// Make the Program class accessible for testing
public partial class Program { }

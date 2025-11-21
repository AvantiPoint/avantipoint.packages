using AvantiPoint.Packages;
using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Configure NuGet Package API with dynamic provider selection
builder.Services.AddNuGetPackageApi(options =>
{
    // Configure Storage Provider based on configuration
    var storageType = builder.Configuration["Storage:Type"] ?? "FileSystem";
    switch (storageType)
    {
        case "FileSystem":
            options.AddFileStorage();
            break;
        case "AzureBlobStorage":
            options.AddAzureBlobStorage();
            break;
        case "AwsS3":
            options.AddAwsS3Storage();
            break;
        default:
            options.AddFileStorage(); // Default fallback
            break;
    }

    // Configure Database Provider based on configuration
    var databaseType = builder.Configuration["Database:Type"] ?? "Sqlite";
    switch (databaseType)
    {
        case "SqlServer":
            options.AddSqlServerDatabase("SqlServer");
            break;
        case "MySql":
            // MySql support requires AvantiPoint.Packages.Database.MySql project reference
            // Uncomment the reference in IntegrationTestApi.csproj and use:
            // options.AddMySqlDatabase("MySql");
            // For now, fall back to Sqlite
            options.AddSqliteDatabase("Sqlite");
            break;
        case "Sqlite":
        default:
            options.AddSqliteDatabase("Sqlite");
            break;
    }

    // Add repository signing support (can be configured via appsettings.json)
    options.AddRepositorySigning();
});

var app = builder.Build();

// Minimal middleware - no UI, no OpenAPI, no static files
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();

// Map only NuGet API routes (no UI routes)
app.MapNuGetApiRoutes();

await app.RunAsync();

// Make the Program class accessible for testing
// Note: This is in the global namespace. Tests should reference IntegrationTestApi project
// and use IntegrationTestApi.Program explicitly, or use a global using alias.
namespace IntegrationTestApi
{
    public partial class Program { }
}


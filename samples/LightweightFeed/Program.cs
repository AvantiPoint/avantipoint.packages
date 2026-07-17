using AvantiPoint.Packages;
using AvantiPoint.Packages.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
});
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseRouting();
app.UseOperationCancelledMiddleware();
app.MapNuGetApiRoutes();
app.MapHealthChecks("/health");

await app.RunMigrationsAsync();
await app.RunAsync();

namespace LightweightFeed
{
    public partial class Program;
}

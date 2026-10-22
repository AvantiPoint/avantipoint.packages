using System.Collections.Concurrent;
using AvantiPoint.Packages.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorPages();
builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPackageDiffService, FixtureDiff>();
var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
var requests = new ConcurrentDictionary<string, int>();
app.MapGet("/v3/package/{id}/{version}/{asset}", async (string id, string version, string asset, HttpContext context) =>
{
    requests.AddOrUpdate($"{id}/{version}/{asset}", 1, (_, n) => n + 1);
    await Task.Delay(700, context.RequestAborted);
    return Results.Text(asset == "license" ? $"Custom terms for {id} {version}\n<script>window.licenseExecuted=true</script>" : $"# Readme {id} {version}\n\nThis is a synthetic fixture.");
});
app.MapGet("/fixture-counts", () => requests);
app.MapRazorPages();
app.MapBlazorHub();
app.Run();

sealed class FixtureDiff : IPackageDiffService
{
    public async Task<PackageDiffResult> GetDiffAsync(string packageId, string currentVersion, string? compareVersion,
        bool onlyOneVersionInFeed, string? filePath = null, string? view = null, string? tfm = null, CancellationToken cancellationToken = default)
    {
        await Task.Delay(700, cancellationToken);
        return new() { PackageId = packageId, CurrentVersion = currentVersion, CompareVersion = compareVersion, Message = "Synthetic diff" };
    }
}

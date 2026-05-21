using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Host.Admin.Extensions;
using AvantiPoint.Packages.Host.Extensions;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.UI;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddNuGetSearchService();
builder.Services.AddHostAdminServices(builder.Configuration);
builder.Services.AddHostIdentityDatabase(builder.Configuration);
builder.Services.AddHostDatabaseHealthChecks();
var authProviders = builder.Configuration.GetSection("Host:Authentication:Providers").Get<string[]>() ?? [];
var useUiAuth = authProviders.Length > 0;
if (useUiAuth)
{
    builder.Services.AddHostAuthentication(builder.Configuration);
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("UI", policy => policy.RequireAuthenticatedUser());
    });
}
else
{
    builder.Services.AddAuthorization();
}

builder.Services.AddOpenApi();
builder.Services.AddNuGetPackageApi(apiOptions =>
{
    apiOptions.AutoDiscoverAwsS3Storage();
    apiOptions.AutoDiscoverAzureBlobStorage();
    apiOptions.AutoDiscoverGcsStorage();
    apiOptions.AutoDiscoverSftpStorage();
    apiOptions.AutoDiscoverFtpStorage();
    apiOptions.AutoDiscoverFileStorage();
    apiOptions.AutoDiscoverPostgreSqlDatabase();
    apiOptions.AutoDiscoverSqliteDatabase();
    apiOptions.AutoDiscoverSqlServerDatabase();
    apiOptions.AutoDiscoverMySqlDatabase();
    apiOptions.AutoDiscoverMariaDb();
    apiOptions.AutoDiscoverElasticsearchSearch();
    apiOptions.AutoDiscoverOpenSearch();
    apiOptions.AutoDiscoverAzureSearch();
});

var app = builder.Build();

await app.InitializeHostDatabasesAsync();

app.MapDefaultEndpoints();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseOperationCancelledMiddleware();
if (useUiAuth)
{
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapRazorPages().RequireAuthorization("UI");
}
else
{
    app.UseAuthorization();
    app.MapRazorPages();
}
app.MapBlazorHub();

app.UseAvantiPointFeedPlatform();
app.MapNuGetApiRoutes();

app.Run();

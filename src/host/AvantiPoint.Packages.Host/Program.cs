using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Extensions;
using AvantiPoint.Packages.Host.Extensions;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.Registry.Npm.Extensions;
using AvantiPoint.Packages.Registry.Oci.Extensions;
using AvantiPoint.Packages.UI;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddNuGetSearchService();
builder.Services.AddHostAdminServices(builder.Configuration);
builder.Services.AddHostDatabaseUpstreamProviders();
builder.Services.AddHostIdentityDatabase(builder.Configuration);
builder.Services.AddHostDatabaseHealthChecks();

var authOptions = builder.Configuration.GetSection("Host:Authentication").Get<HostAuthenticationOptions>()
    ?? new HostAuthenticationOptions();
var useUiAuth = HostAuthenticationResolver.TryResolve(authOptions) is not null;

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

var feedSection = builder.Configuration.GetSection("Feed");
var feed = builder.AddAvantiPointFeed(feedSection);
feed.UseNuGet();
feed.UseNpmRegistryIfEnabled(feedSection.GetSection("Npm"));
feed.UseConfiguredOciSurfaces(feedSection.GetSection("Oci"));

if (feed.Registry.TryGetNpmSurface() is not null)
{
    builder.Services.AddNpmPackageBrowseUi();
}

if (feed.Registry.Surfaces.Any(static s => s.Protocol == FeedProtocol.Oci))
{
    builder.Services.AddOciRepositoryBrowseUi();
}

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
app.UseAvantiPointFeedPlatform();
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

app.MapNuGetApiRoutes();
app.MapNpmFeed(feed);
app.MapOciFeed(feed);

app.Run();

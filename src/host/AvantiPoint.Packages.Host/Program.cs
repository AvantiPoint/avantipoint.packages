using AvantiPoint.Feed.Platform;
using AvantiPoint.Feed.Platform.Extensions;
using AvantiPoint.Feed.Platform.Health;
using AvantiPoint.Packages;
using AvantiPoint.Packages.Host.Admin.Authentication;
using AvantiPoint.Packages.Host.Admin.Configuration;
using AvantiPoint.Packages.Host.Admin.Extensions;
using AvantiPoint.Packages.Host.Extensions;
using AvantiPoint.Packages.Host.Services;
using AvantiPoint.Packages.UI.Services;
using AvantiPoint.Packages.Hosting;
using AvantiPoint.Packages.Registry.Npm.Extensions;
using AvantiPoint.Packages.Registry.Oci.Extensions;
using AvantiPoint.Packages.Registry.Oci;
using AvantiPoint.Packages.UI;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
HostSecurityConfiguration.Validate(builder.Configuration, builder.Environment.IsDevelopment());

builder.AddServiceDefaults();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
var reverseProxyOptions = builder.Configuration
    .GetSection(HostReverseProxyOptions.SectionName)
    .Get<HostReverseProxyOptions>() ?? new HostReverseProxyOptions();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    reverseProxyOptions.Apply(options);
});
var configuredPathBase = HostPathBase.Resolve(builder.Configuration);
builder.Services.AddScoped<INuGetSearchService, InProcessNuGetSearchService>();
builder.Services.AddPackageDiffService();
builder.Services.AddScoped<DevelopmentDemoDataSeeder>();
builder.Services.AddHostAdminServices(builder.Configuration);
builder.Services.AddHostDatabaseUpstreamProviders();
builder.Services.AddHostIdentityDatabase(builder.Configuration);
builder.Services.AddHostDatabaseHealthChecks();
builder.Services.AddHostDependencyHealthChecks();
builder.Services.AddHostRequestLimits(builder.Configuration);
builder.Services.Configure<OciGarbageCollectionOptions>(
    builder.Configuration.GetSection("Feed:Operations:OciGarbageCollection"));

var authOptions = builder.Configuration.GetSection("Host:Authentication").Get<HostAuthenticationOptions>()
    ?? new HostAuthenticationOptions();
var useUiAuth = HostAuthenticationResolver.TryResolve(authOptions) is not null;
var allowAnonymousPull = builder.Configuration.GetValue<bool>("Feed:Authentication:AllowAnonymousPull");

if (useUiAuth)
{
    builder.Services.AddHostAuthentication(builder.Configuration);
}
else
{
    // Account pages carry role-based [Authorize] attributes even when no external login
    // provider is configured. Keep a local cookie challenge scheme registered so direct
    // requests redirect to the explanatory login page instead of throwing because no
    // authentication handler exists.
    builder.Services
        .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(cookie =>
        {
            cookie.LoginPath = "/Account/Login";
            cookie.AccessDeniedPath = "/Account/AccessDenied";
            cookie.Events.OnValidatePrincipal = HostCookiePrincipalValidator.ValidateAsync;
        });
}

builder.Services.AddAuthorization(options =>
    options.AddPolicy(
        HostUiAuthorizationPolicy.Name,
        HostUiAuthorizationPolicy.Create(allowAnonymousPull)));

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

builder.Services.AddArtifactDiscoveryUi();

var app = builder.Build();

await app.InitializeHostDatabasesAsync();

if (app.Environment.IsDevelopment()
    && app.Configuration.GetValue<bool>("Host:DemoData:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider
        .GetRequiredService<DevelopmentDemoDataSeeder>()
        .SeedAsync();
}

app.UseForwardedHeaders();
if (configuredPathBase.HasValue)
{
    // UsePathBase supports proxies that preserve the public prefix. The fallback assignment
    // supports proxies that strip it before forwarding while keeping generated UI/API links
    // rooted at the configured public mount path.
    app.UsePathBase(configuredPathBase);
    app.Use((context, next) =>
    {
        if (!context.Request.PathBase.HasValue)
        {
            context.Request.PathBase = configuredPathBase;
        }

        return next(context);
    });
}

app.MapDefaultEndpoints();
if (!app.Environment.IsDevelopment())
{
    // ServiceDefaults intentionally limits its generic readiness endpoint to Development.
    // Packages.Host documents and uses /health as its production container readiness probe.
    app.MapHealthChecks("/health").DisableRateLimiting();
    app.MapHealthChecks("/alive", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("live"),
    }).DisableRateLimiting();
}
app.MapFeedHealthEndpoints();
app.MapMetrics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue("Host:HttpsRedirection:Enabled", true))
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseAvantiPointFeedPlatform();
app.UseRouting();
app.UseRateLimiter();
app.UseOperationCancelledMiddleware();
app.UseAuthentication();
app.UseAuthorization();
if (useUiAuth)
{
    app.MapGet("/Account/Logout", async (HttpContext context) =>
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Redirect(context.Request.PathBase.Add("/Account/Login").Value!);
    }).AllowAnonymous();
}
app.MapRazorPages().RequireAuthorization(HostUiAuthorizationPolicy.Name);
app.MapBlazorHub().RequireAuthorization(HostUiAuthorizationPolicy.Name);

app.MapNuGetApiRoutes();
app.MapNpmFeed(feed);
app.MapOciFeed(feed);

app.Run();

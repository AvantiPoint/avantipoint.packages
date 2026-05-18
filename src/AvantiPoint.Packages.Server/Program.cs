using AvantiPoint.Packages;
using AvantiPoint.Packages.Server.Configuration;
using AvantiPoint.Packages.Server.Extensions;
using AvantiPoint.Packages.UI;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var options = builder.Configuration.Get<ServerOptions>() ?? new ServerOptions();

// Conditionally add UI and authentication
if (options.UseNuGetUI)
{
    // Add Razor Pages and Blazor Server for components
    builder.Services.AddRazorPages();
    builder.Services.AddServerSideBlazor();
    
    // Add search service for UI
    builder.Services.AddNuGetSearchService();

    // Configure authentication based on AuthType
    if (options.Authentication.AuthType == AuthOptions.Microsoft)
    {
        var microsoftAuth = builder.Configuration.GetSection("Authentication:Microsoft").Get<MicrosoftAuthOptions>()
            ?? throw new InvalidOperationException("Microsoft authentication is enabled but configuration is missing.");
        
        builder.Services.AddMicrosoftAuthentication(builder.Configuration, microsoftAuth);
        
        // Require authentication for UI pages only (not API routes)
        builder.Services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy("UI", policy => policy.RequireAuthenticatedUser());
        });
    }
    else if (options.Authentication.AuthType == AuthOptions.Google)
    {
        var googleAuth = builder.Configuration.GetSection("Authentication:Google").Get<GoogleAuthOptions>()
            ?? throw new InvalidOperationException("Google authentication is enabled but configuration is missing.");
        
        builder.Services.AddGoogleAuthentication(builder.Configuration, googleAuth);
        
        // Require authentication for UI pages only (not API routes)
        builder.Services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy("UI", policy => policy.RequireAuthenticatedUser());
        });
    }
    // If AuthType is None, no authentication is required
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
});

var app = builder.Build();

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Conditionally enable authentication middleware
if (options.UseNuGetUI && options.Authentication.AuthType != AuthOptions.None)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

// Conditionally map UI routes
if (options.UseNuGetUI)
{
    if (options.Authentication.AuthType != AuthOptions.None)
    {
        // Require authentication for UI pages
        app.MapRazorPages().RequireAuthorization("UI");
    }
    else
    {
        // No authentication required
        app.MapRazorPages();
    }
    app.MapBlazorHub();
}

// Always map API routes (these are separate from UI)
app.MapNuGetApiRoutes();

app.Run();

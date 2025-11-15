# Callbacks and Event Handlers

AvantiPoint Packages allows you to react to package lifecycle events through the `INuGetFeedActionHandler` interface. This is useful for:

- Sending email notifications when packages are published
- Collecting download metrics and analytics
- Restricting access to specific packages based on user licenses
- Monitoring for security concerns
- Integrating with external systems

## The INuGetFeedActionHandler Interface

```csharp
public interface INuGetFeedActionHandler
{
    Task<bool> CanDownloadPackage(string packageId, string version);
    Task OnPackageDownloaded(string packageId, string version);
    Task OnSymbolsDownloaded(string packageId, string version);
    Task OnPackageUploaded(string packageId, string version);
    Task OnSymbolsUploaded(string packageId, string version);
}
```

### Methods

- **CanDownloadPackage** - Called before a package is downloaded. Return `false` to deny access to specific packages.
- **OnPackageDownloaded** - Called after a package is successfully downloaded.
- **OnSymbolsDownloaded** - Called after symbol files are successfully downloaded.
- **OnPackageUploaded** - Called after a package is successfully uploaded.
- **OnSymbolsUploaded** - Called after symbol files are successfully uploaded.

## Basic Implementation

Here's a simple implementation that logs all events:

```csharp
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.Extensions.Logging;

public class LoggingActionHandler : INuGetFeedActionHandler
{
    private readonly ILogger<LoggingActionHandler> _logger;
    
    public LoggingActionHandler(ILogger<LoggingActionHandler> logger)
    {
        _logger = logger;
    }
    
    public Task<bool> CanDownloadPackage(string packageId, string version)
    {
        _logger.LogInformation("Checking access to {PackageId} {Version}", packageId, version);
        return Task.FromResult(true);
    }
    
    public Task OnPackageDownloaded(string packageId, string version)
    {
        _logger.LogInformation("Package downloaded: {PackageId} {Version}", packageId, version);
        return Task.CompletedTask;
    }
    
    public Task OnSymbolsDownloaded(string packageId, string version)
    {
        _logger.LogInformation("Symbols downloaded: {PackageId} {Version}", packageId, version);
        return Task.CompletedTask;
    }
    
    public Task OnPackageUploaded(string packageId, string version)
    {
        _logger.LogInformation("Package uploaded: {PackageId} {Version}", packageId, version);
        return Task.CompletedTask;
    }
    
    public Task OnSymbolsUploaded(string packageId, string version)
    {
        _logger.LogInformation("Symbols uploaded: {PackageId} {Version}", packageId, version);
        return Task.CompletedTask;
    }
}
```

## Accessing the Current User

If you've implemented authentication, you can access the authenticated user's claims in your action handler:

```csharp
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Http;

public class UserAwareActionHandler : INuGetFeedActionHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public UserAwareActionHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public Task<bool> CanDownloadPackage(string packageId, string version)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            // Allow anonymous access or deny based on your requirements
            return Task.FromResult(true);
        }
        
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        
        // Example: Check if user has access to this specific package
        // This could query a database, check licenses, etc.
        bool hasAccess = CheckUserLicense(email, packageId);
        
        return Task.FromResult(hasAccess);
    }
    
    public Task OnPackageDownloaded(string packageId, string version)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var username = user?.FindFirst(ClaimTypes.Name)?.Value ?? "anonymous";
        
        // Record download metrics
        RecordDownloadMetric(username, packageId, version);
        
        return Task.CompletedTask;
    }
    
    // ... other methods
    
    private bool CheckUserLicense(string email, string packageId)
    {
        // Your license checking logic here
        return true;
    }
    
    private void RecordDownloadMetric(string username, string packageId, string version)
    {
        // Your metrics collection logic here
    }
}
```

## Email Notifications

Send emails when packages are uploaded:

```csharp
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Linq;

public class EmailNotificationHandler : INuGetFeedActionHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IEmailService _emailService;
    
    public EmailNotificationHandler(
        IHttpContextAccessor httpContextAccessor,
        IEmailService emailService)
    {
        _httpContextAccessor = httpContextAccessor;
        _emailService = emailService;
    }
    
    public Task<bool> CanDownloadPackage(string packageId, string version)
    {
        return Task.FromResult(true);
    }
    
    public Task OnPackageDownloaded(string packageId, string version)
    {
        return Task.CompletedTask;
    }
    
    public Task OnSymbolsDownloaded(string packageId, string version)
    {
        return Task.CompletedTask;
    }
    
    public async Task OnPackageUploaded(string packageId, string version)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var username = user?.FindFirst(ClaimTypes.Name)?.Value;
        var email = user?.FindFirst(ClaimTypes.Email)?.Value;
        
        if (!string.IsNullOrEmpty(email))
        {
            await _emailService.SendAsync(
                to: email,
                subject: $"Package Published: {packageId} {version}",
                body: $"Your package {packageId} version {version} was successfully published."
            );
        }
    }
    
    public async Task OnSymbolsUploaded(string packageId, string version)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var email = user?.FindFirst(ClaimTypes.Email)?.Value;
        
        if (!string.IsNullOrEmpty(email))
        {
            await _emailService.SendAsync(
                to: email,
                subject: $"Symbols Published: {packageId} {version}",
                body: $"Symbol files for {packageId} version {version} were successfully published."
            );
        }
    }
}
```

## Collecting Download Metrics

Track which packages are being downloaded and by whom:

```csharp
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

public class MetricsCollectionHandler : INuGetFeedActionHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMetricsRepository _metricsRepo;
    
    public MetricsCollectionHandler(
        IHttpContextAccessor httpContextAccessor,
        IMetricsRepository metricsRepo)
    {
        _httpContextAccessor = httpContextAccessor;
        _metricsRepo = metricsRepo;
    }
    
    public Task<bool> CanDownloadPackage(string packageId, string version)
    {
        return Task.FromResult(true);
    }
    
    public async Task OnPackageDownloaded(string packageId, string version)
    {
        var context = _httpContextAccessor.HttpContext;
        var user = context?.User;
        var ipAddress = context?.Connection?.RemoteIpAddress?.ToString();
        
        await _metricsRepo.RecordDownloadAsync(new DownloadMetric
        {
            PackageId = packageId,
            Version = version,
            Username = user?.FindFirst(ClaimTypes.Name)?.Value,
            Email = user?.FindFirst(ClaimTypes.Email)?.Value,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
    }
    
    // ... other methods
}
```

## Restricting Package Access

Control which packages users can download based on their license or subscription:

```csharp
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AvantiPoint.Packages.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

public class LicenseBasedAccessHandler : INuGetFeedActionHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MyDbContext _db;
    
    public LicenseBasedAccessHandler(
        IHttpContextAccessor httpContextAccessor,
        MyDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _db = db;
    }
    
    public async Task<bool> CanDownloadPackage(string packageId, string version)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return false; // Require authentication
        }
        
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            return false;
        }
        
        // Check if user has an active license for this package
        var hasLicense = await _db.UserLicenses
            .AnyAsync(x => 
                x.UserEmail == email && 
                x.PackageId == packageId &&
                x.IsActive &&
                x.ExpiresAt > DateTime.UtcNow);
        
        return hasLicense;
    }
    
    // ... other methods return Task.CompletedTask
}
```

## Registering Your Handler

In your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register HttpContextAccessor if your handler needs it
builder.Services.AddHttpContextAccessor();

// Register your action handler
builder.Services.AddScoped<INuGetFeedActionHandler, YourActionHandler>();

// Configure the package API
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
});
```

## Multiple Handlers

You can implement multiple interfaces or combine logic in a single handler. For complex scenarios, consider using a composite pattern to chain multiple handlers.

## Important Notes

- The `ClaimsPrincipal` available in the `HttpContext` is the one you returned from `IPackageAuthenticationService`.
- If no authentication service is registered, the user will be anonymous.
- `CanDownloadPackage` is called before the package download begins. Returning `false` will result in a 403 Forbidden response.
- All other methods are called asynchronously after the operation completes.
- Exceptions thrown in handlers will be logged but won't fail the operation for the client.

## See Also

- [Authentication](authentication.md) - Set up user authentication
- [Sample: AuthenticatedFeed](../samples/AuthenticatedFeed) - Complete working example with callbacks

---
id: authentication
title: Authentication
sidebar_label: Authentication
sidebar_position: 8
---

AvantiPoint Packages provides flexible authentication to secure your NuGet feed. You control who can access packages and who can publish them.

## Understanding NuGet Authentication

NuGet clients support two authentication methods:

1. **Basic Authentication** - Used when consuming packages (downloading). The client sends a username and password/token.
2. **API Key Authentication** - Used when publishing packages. The client sends an API key via the `X-NuGet-ApiKey` header.

AvantiPoint Packages uses a dual-role model:
- **Package Consumer** - Can browse and download packages
- **Package Publisher** - Can upload new packages and symbols

## Implementing Authentication

To add authentication, implement the `IPackageAuthenticationService` interface and register it in your dependency injection container.

### IPackageAuthenticationService Interface

```csharp
public interface IPackageAuthenticationService
{
    // Called when publishing packages (API key authentication)
    Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);
    
    // Called when consuming packages (basic authentication)
    Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken);
}
```

### Basic Implementation

Here's a simple example that checks against hardcoded credentials:

```csharp
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;

public class SimpleAuthenticationService : IPackageAuthenticationService
{
    public Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        // Validate API key for publishing
        if (apiKey == "your-secret-api-key")
        {
            var identity = new ClaimsIdentity("NuGetAuth");
            identity.AddClaim(new Claim(ClaimTypes.Name, "Publisher"));
            
            return Task.FromResult(
                NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity))
            );
        }
        
        return Task.FromResult(
            NuGetAuthenticationResult.Fail("Invalid API key", "My Feed")
        );
    }
    
    public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
    {
        // Validate username/token for consuming
        if (username == "consumer@example.com" && token == "consumer-token")
        {
            var identity = new ClaimsIdentity("NuGetAuth");
            identity.AddClaim(new Claim(ClaimTypes.Name, username));
            identity.AddClaim(new Claim(ClaimTypes.Email, username));
            
            return Task.FromResult(
                NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity))
            );
        }
        
        return Task.FromResult(
            NuGetAuthenticationResult.Fail("Invalid credentials", "My Feed")
        );
    }
}
```

### Database-Backed Authentication

For production use, you'll want to validate credentials against a database:

```csharp
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AvantiPoint.Packages.Core;
using Microsoft.EntityFrameworkCore;

public class DatabaseAuthenticationService : IPackageAuthenticationService
{
    private readonly MyDbContext _db;
    
    public DatabaseAuthenticationService(MyDbContext db)
    {
        _db = db;
    }
    
    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        var token = await _db.ApiTokens
            .Include(x => x.User)
            .ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Token == apiKey && !x.IsRevoked, cancellationToken);
        
        if (token is null || token.ExpiresAt < DateTime.UtcNow)
        {
            return NuGetAuthenticationResult.Fail("Invalid or expired API key", "My Feed");
        }
        
        // Check if user has publisher permission
        if (!token.User.Permissions.Any(x => x.Name == "PackagePublisher"))
        {
            return NuGetAuthenticationResult.Fail("User is not authorized to publish packages", "My Feed");
        }
        
        var identity = new ClaimsIdentity("NuGetAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, token.User.Email));
        
        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }
    
    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
    {
        var apiToken = await _db.ApiTokens
            .Include(x => x.User)
            .ThenInclude(x => x.Permissions)
            .FirstOrDefaultAsync(x => x.Token == token && x.User.Email == username && !x.IsRevoked, cancellationToken);
        
        if (apiToken is null || apiToken.ExpiresAt < DateTime.UtcNow)
        {
            return NuGetAuthenticationResult.Fail("Invalid or expired credentials", "My Feed");
        }
        
        // Check if user has consumer permission
        if (!apiToken.User.Permissions.Any(x => x.Name == "PackageConsumer"))
        {
            return NuGetAuthenticationResult.Fail("User is not authorized to access packages", "My Feed");
        }
        
        var identity = new ClaimsIdentity("NuGetAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, apiToken.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, apiToken.User.Email));
        
        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }
}
```

## Registering Your Authentication Service

In your `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register your authentication service
builder.Services.AddScoped<IPackageAuthenticationService, DatabaseAuthenticationService>();

// Configure the package API
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
});

var app = builder.Build();
// ... rest of configuration
```

## Using the Feed

### For Consumers

Add the feed with credentials:

```bash
# Configure the source
dotnet nuget add source https://my-feed.example.com/v3/index.json --name MyFeed

# Add credentials
dotnet nuget update source MyFeed --username consumer@example.com --password consumer-token
```

Or use `nuget.config`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="MyFeed" value="https://my-feed.example.com/v3/index.json" />
  </packageSources>
  <packageSourceCredentials>
    <MyFeed>
      <add key="Username" value="consumer@example.com" />
      <add key="ClearTextPassword" value="consumer-token" />
    </MyFeed>
  </packageSourceCredentials>
</configuration>
```

### For Publishers

Push packages with your API key:

```bash
dotnet nuget push MyPackage.1.0.0.nupkg --source MyFeed --api-key your-secret-api-key
```

## Important Notes

- AvantiPoint Packages does **not** use ASP.NET Core's built-in authentication. The `ClaimsPrincipal` you provide is only for your own use (e.g., in callbacks).
- Always use HTTPS in production to protect credentials in transit.
- Consider implementing token expiration and rotation for better security.
- Store API keys hashed, never in plain text.

## See Also

- [Callbacks](callbacks.md) - Access the authenticated user in event handlers
- [Sample: AuthenticatedFeed](https://github.com/AvantiPoint/avantipoint.packages/tree/master/samples/AuthenticatedFeed) - Complete working example

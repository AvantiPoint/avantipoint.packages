# NuGet Package Search Component

A reusable Blazor component for searching and displaying NuGet packages from any NuGet v3 feed.

## Features

- 🔍 Full-text package search with pagination
- 🎨 Modern, responsive UI inspired by nuget.org
- 🔐 Supports authenticated and unauthenticated feeds
- 🌐 Works with same-site or cross-origin feeds
- ⚡ Multiple implementation options (HTTP, Protocol client)
- 📦 Prerelease package filtering
- 🎯 Customizable result click handling

## Installation

Add a reference to the `AvantiPoint.Packages.UI.Razor` package:

```xml
<PackageReference Include="AvantiPoint.Packages.UI.Razor" />
```

## Quick Start

### Option 1: HTTP-based (Recommended for External Feeds)

Use this approach when connecting to an external NuGet feed (cross-origin) or when you need explicit control over authentication.

```csharp
// Program.cs or Startup.cs
builder.Services.AddScoped<INuGetSearchService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    
    // For public feeds (like nuget.org)
    return new HttpNuGetSearchService(
        httpClient,
        "https://api-v2v3search-0.nuget.org/query"); // NuGet.org search endpoint
    
    // For authenticated feeds
    // return new HttpNuGetSearchService(
    //     httpClient,
    //     "https://your-feed.example.com/v3/search",
    //     authTokenProvider: async () => await GetUserApiKeyAsync());
});
```

### Option 2: Same-Site Feed (Relative Paths)

When the component is embedded in the same web application as your NuGet feed:

```csharp
// Program.cs
builder.Services.AddScoped<INuGetSearchService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    
    // Use relative path - same origin, no CORS issues
    return new HttpNuGetSearchService(
        httpClient,
        "/v3/search"); // Relative path to your local feed
});
```

### Option 3: Protocol Client (Advanced)

When you want to use the built-in `NuGetClient` from `AvantiPoint.Packages.Protocol`:

```csharp
// Program.cs
builder.Services.AddScoped<INuGetSearchService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var client = new NuGetClient("https://api.nuget.org/v3/index.json", httpClient);
    
    return new ProtocolNuGetSearchService(client);
});
```

## Usage in Razor Components

```razor
@page "/packages"
@using AvantiPoint.Packages.UI.Razor.Components

<PackageSearch OnPackageSelected="HandlePackageSelected" />

@code {
    private void HandlePackageSelected(SearchResult package)
    {
        // Handle package selection
        NavigationManager.NavigateTo($"/packages/{package.PackageId}");
    }
}
```

## Authentication Scenarios

### API Key Authentication

```csharp
services.AddScoped<INuGetSearchService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    
    return new HttpNuGetSearchService(
        httpClient,
        "https://your-feed.example.com/v3/search",
        authTokenProvider: async () => 
        {
            // Return API key from configuration, user session, etc.
            var apiKey = await GetCurrentUserApiKey();
            return apiKey;
        });
});
```

### Bearer Token Authentication

```csharp
services.AddScoped<INuGetSearchService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    
    return new HttpNuGetSearchService(
        httpClient,
        "https://your-feed.example.com/v3/search",
        authTokenProvider: async () => 
        {
            var token = await GetAuthTokenAsync();
            return $"Bearer {token}";
        });
});
```

### User-Specific Authentication (Session-based)

```csharp
services.AddScoped<INuGetSearchService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
    
    return new HttpNuGetSearchService(
        httpClient,
        "/v3/search",
        authTokenProvider: async () => 
        {
            // Get token from current user's session/claims
            var user = httpContextAccessor.HttpContext?.User;
            var apiKey = user?.FindFirst("nuget-api-key")?.Value;
            return apiKey;
        });
});
```

## Component Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Placeholder` | string | "Search packages..." | Placeholder text for search input |
| `OnPackageSelected` | EventCallback<SearchResult> | - | Callback when user clicks a package |
| `ResultsPerPage` | int | 20 | Number of results per page |

## Customization

### Custom Styling

The component uses scoped CSS. Override styles by targeting the component classes:

```css
::deep .nuget-package-search {
    /* Your custom styles */
}

::deep .package-item {
    border-color: your-brand-color;
}
```

### Custom Package Item Template

Extend `PackageSearchResultItem.razor` or create your own:

```razor
@inherits PackageSearchResultItem

<!-- Your custom package display -->
```

## CORS Considerations

When connecting to external feeds, ensure CORS is properly configured:

```csharp
// On the NuGet feed server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSearchClients", policy =>
    {
        policy.WithOrigins("https://your-app.example.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

## Examples

### Multiple Feed Support

```csharp
// Define multiple feeds
public enum FeedType { NuGetOrg, Internal, Sponsor }

services.AddScoped<Func<FeedType, INuGetSearchService>>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    
    return feedType => feedType switch
    {
        FeedType.NuGetOrg => new HttpNuGetSearchService(
            httpClientFactory.CreateClient(),
            "https://api-v2v3search-0.nuget.org/query"),
            
        FeedType.Internal => new HttpNuGetSearchService(
            httpClientFactory.CreateClient(),
            "https://internal.example.com/v3/search",
            () => Task.FromResult<string?>("your-api-key")),
            
        FeedType.Sponsor => new HttpNuGetSearchService(
            httpClientFactory.CreateClient(),
            "https://sponsors.example.com/v3/search",
            async () => await GetSponsorTokenAsync()),
            
        _ => throw new ArgumentException("Unknown feed type")
    };
});
```

## Troubleshooting

### CORS Errors
- Ensure the NuGet feed server has CORS enabled for your origin
- Check browser console for specific CORS error messages
- Verify the search endpoint URL is correct

### Authentication Failures
- Verify the token provider returns a valid token
- Check if the feed requires `X-NuGet-ApiKey` header or `Authorization: Bearer`
- Test the endpoint with curl/Postman to confirm auth requirements

### No Results
- Verify the search endpoint URL is correct
- Check network tab to see the actual API response
- Ensure the feed has indexed packages

## License

This component is part of the AvantiPoint.Packages project and follows the same license.

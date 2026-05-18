---
sidebar_position: 15
---

# UI Components

AvantiPoint Packages provides reusable UI components for building rich NuGet package browsing and search experiences in your applications. These components are designed to integrate seamlessly with your NuGet feed, providing a modern, accessible interface for package discovery.

## Available Component Libraries

### Blazor / Razor Components

**Package:** `AvantiPoint.Packages.UI.Razor`  
**Status:** ✅ Available  
**Target Framework:** .NET 10.0

The Blazor/Razor component library provides production-ready components that work with:
- **Blazor Server** applications
- **Blazor WebAssembly** applications
- **Razor Pages** (.cshtml)
- **MVC Views** (.cshtml)

### React Components

**Package:** `@avantipoint/packages-ui-react`  
**Status:** 🚧 Coming Soon

React component library for building NuGet package browsers in React applications. This will provide the same functionality as the Blazor components but with a React-first API.

### Angular Components

**Package:** `@avantipoint/packages-ui-angular`  
**Status:** 🚧 Coming Soon

Angular component library for integrating NuGet package search and display into Angular applications.

## Blazor / Razor Components

### Installation

Add the package to your project:

```bash
dotnet add package AvantiPoint.Packages.UI.Razor
```

### Quick Start

#### 1. Register Services

In your `Program.cs`, register the required services:

```csharp
using AvantiPoint.Packages.UI;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Components or Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// or
// builder.Services.AddServerSideBlazor();

// Register NuGet search service
// Default: Uses the same host/project (/v3/index.json endpoint)
builder.Services.AddNuGetSearchService();

var app = builder.Build();
```

**Configuration Options:**

By default, `AddNuGetSearchService()` assumes the NuGet API endpoints are **part of the same application/host**. It automatically discovers endpoints from the current host's `/v3/index.json`.

For **external feeds** or **separate API hosts**, configure the service index URL:

```csharp
// Point to an external NuGet feed (e.g., nuget.org)
builder.Services.AddNuGetSearchService(options =>
{
    options.ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
});

// Or point to a separate internal feed on another host
builder.Services.AddNuGetSearchService(options =>
{
    options.ServiceIndexUrl = "https://packages.internal.company.com/v3/index.json";
});
```

For **authenticated private feeds**, configure authentication headers based on the current user:

```csharp
builder.Services.AddNuGetSearchService(options =>
{
    options.ServiceIndexUrl = "https://private-feed.company.com/v3/index.json";
    
    // Configure per-request authentication
    options.ConfigureHttpClient = (httpContext, httpClient) =>
    {
        // Example: Pass through authenticated user's credentials
        var username = httpContext.User.Identity?.Name;
        var token = httpContext.Items["NuGetApiKey"] as string;
        
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{username}:{token}"));
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);
        }
    };
});
```

This allows the UI components to **verify the current user has access** to the feed and show packages based on their permissions.

#### 2. Add Component Imports

In your `_Imports.razor` (Blazor) or page-level imports (Razor Pages/MVC):

```razor
@using AvantiPoint.Packages.UI.Components
```

#### 3. Use Components

**In Blazor:**

```razor
@page "/packages"
@using AvantiPoint.Packages.UI.Components

<PackageSearch OnPackageSelected="HandlePackageSelected" />

@code {
    private void HandlePackageSelected(SearchResult package)
    {
        // Navigate or display package details
    }
}
```

**In Razor Pages (.cshtml):**

```cshtml
@page
@model PackagesModel

<component type="typeof(PackageSearch)" 
           render-mode="ServerPrerendered" 
           param-OnPackageSelected="@Model.HandlePackageSelected" />
```

**In MVC Views:**

```cshtml
@using AvantiPoint.Packages.UI.Components

<component type="typeof(PackageSearch)" 
           render-mode="Server" />
```

## Component Reference

### PackageSearch

Full-featured package search component with advanced filtering, pagination, and framework targeting.

**Features:**
- Full-text search with query suggestions
- Advanced framework filters (.NET, .NET Core, .NET Standard, .NET Framework)
- Package type filtering (Dependency, .NET Tool, Template, MCP Server)
- Prerelease package inclusion
- Framework compatibility matching
- Responsive design with mobile-friendly controls
- Skeleton loading states
- Accessible ARIA attributes

**Props:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Placeholder` | `string` | `"Search packages..."` | Search input placeholder text |
| `OnPackageSelected` | `EventCallback<SearchResult>` | - | Callback when a package is clicked |
| `PackageSelected` | `EventCallback<SearchResult>` | - | Alternative callback for package selection |
| `ResultsPerPage` | `int` | `20` | Number of results per page |

**Usage Example:**

```razor
<PackageSearch 
    Placeholder="Find a package..."
    ResultsPerPage="25"
    OnPackageSelected="@HandleSelection" />

@code {
    private async Task HandleSelection(SearchResult package)
    {
        NavigationManager.NavigateTo($"/packages/{package.Id}");
    }
}
```

**Advanced Filters:**

The component includes collapsible advanced search filters:

- **Frameworks:** Multi-select framework targeting with group selection
  - .NET (net10.0, net9.0, net8.0, etc.)
  - .NET Core (netcoreapp3.1, netcoreapp2.1, etc.)
  - .NET Standard (netstandard2.1, netstandard2.0, etc.)
  - .NET Framework (net481, net472, net462, etc.)
- **Framework Filter Mode:** ALL (package must support all selected) or ANY (package supports any selected)
- **Include Compatible Frameworks:** Automatically expand to compatible TFMs
- **Package Type:** All types, Dependency, .NET tool, Template, MCP Server
- **Options:** Include prerelease versions

### PackageDetail

Comprehensive package detail view with installation instructions, readme rendering, dependency visualization, and download statistics.

**Features:**
- Version picker with grouped versions (by major version)
- Multiple installation formats (.NET CLI, Package Manager, PackageReference, Central Package Management)
- Markdown readme rendering with syntax highlighting
- Dependencies and frameworks display
- Download statistics (total, current version, per day)
- Badge generation (Shields.io compatible)
- Project, repository, and license links
- Package/symbol download links
- Tag navigation

**Props:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Package` | `PackageInfoCollection` | **Required** | Package metadata to display |
| `FeedBaseUrl` | `string?` | Current base URL | Base URL for feed API calls |
| `FeedDisplayName` | `string?` | Auto-detected | Display name for badge generation |
| `AllowPackageDownloads` | `bool` | `true` | Enable/disable download links |
| `ProjectUrlTitle` | `string` | `"Project Site"` | Title for project URL link |
| `DefaultIconPath` | `string` | Default icon | Fallback icon path |
| `OnVersionSelected` | `EventCallback<NuGetVersion>` | - | Callback when version changes |
| `GetLocalPackageUrl` | `Func<string, Task<string?>>?` | - | Custom URL resolver for local packages |

**Usage Example:**

```razor
@inject IPackageMetadataService MetadataService

<PackageDetail 
    Package="@packageInfo"
    FeedBaseUrl="https://packages.example.com"
    FeedDisplayName="My Private Feed"
    AllowPackageDownloads="true"
    OnVersionSelected="@HandleVersionChange" />

@code {
    private PackageInfoCollection packageInfo = null!;
    
    protected override async Task OnInitializedAsync()
    {
        packageInfo = await MetadataService.GetPackageInfoAsync("Newtonsoft.Json");
    }
    
    private async Task HandleVersionChange(NuGetVersion version)
    {
        await JSRuntime.InvokeVoidAsync("console.log", $"Version changed to {version}");
    }
}
```

**Installation Tab Formats:**

1. **.NET CLI:** `dotnet add package` command
2. **Package Manager:** `Install-Package` PowerShell command
3. **PackageReference:** XML snippet for .csproj files
4. **Central Package Management:** Separate snippets for `Directory.Packages.props` and `.csproj`

**Content Tabs:**

1. **ReadMe:** Rendered markdown from package readme
2. **References and Dependencies:** Grouped by target framework
3. **Badges:** Preview and markdown/HTML snippets for package badges

### PackageSearchResultItem

Individual search result card displaying package summary information.

**Features:**
- Package icon with fallback
- Package ID and version
- Authors and description
- Download count badge
- Tags
- Deprecated package warnings
- Prerelease/unlisted indicators

**Usage:**

This component is used internally by `PackageSearch` but can be used standalone:

```razor
<PackageSearchResultItem 
    Package="@searchResult" 
    OnPackageClick="@HandleClick" />
```

### Supporting Components

#### PackageSearchResultSkeleton

Loading skeleton for search results during async operations.

#### DependencyRow

Individual dependency row in the package detail dependency table.

#### FeedInfo

Displays feed information and metadata (customizable feed branding).

## Styling and Theming

### Built-in Styles

The component library includes comprehensive CSS that follows modern design principles:

- **CSS Scoping:** All styles are scoped under `.nuget-theme-scope` to avoid conflicts
- **Responsive Design:** Mobile-first approach with breakpoints for tablets and desktops
- **Accessibility:** WCAG 2.1 AA compliant with proper ARIA attributes
- **Dark Mode:** Automatically respects system color scheme preferences

### Custom Styling

The components use CSS custom properties (variables) that can be overridden:

```css
.nuget-theme-scope {
    --nuget-primary-color: #0078d4;
    --nuget-background-color: #ffffff;
    --nuget-text-color: #323130;
    --nuget-border-color: #edebe9;
    --nuget-hover-color: #f3f2f1;
}
```

### CSS Classes

All major elements have semantic CSS classes for custom styling:

- `.nuget-package-search` - Search container
- `.search-bar-row` - Search input row
- `.advanced-search-panel` - Advanced filter panel
- `.package-detail` - Package detail container
- `.install-snippet` - Installation code snippets
- `.package-readme` - Readme content area
- `.dependency-table` - Dependency tables

## Configuration

### Search Service Configuration

**Default Behavior:**

By default, `AddNuGetSearchService()` is designed for scenarios where the **NuGet API and UI are part of the same application**. The service automatically discovers endpoints from the current request's host using `/v3/index.json` (relative path).

This is the typical setup when:
- You're building a self-hosted NuGet feed with a built-in UI
- The API and UI share the same domain/host
- No external HTTP calls are needed (same-process API calls)

**External Feed Configuration:**

When your UI needs to connect to a **separate NuGet feed** (different host), configure the full service index URL:

```csharp
builder.Services.AddNuGetSearchService(options =>
{
    // Connect to nuget.org
    options.ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    
    // Or connect to an internal feed on another host
    // options.ServiceIndexUrl = "https://packages.internal.company.com/v3/index.json";
});
```

**Authenticated Feed Configuration:**

For **private feeds requiring authentication**, use the `ConfigureHttpClient` callback to add per-user authentication:

```csharp
builder.Services.AddNuGetSearchService(options =>
{
    options.ServiceIndexUrl = "https://private-feed.company.com/v3/index.json";
    
    options.ConfigureHttpClient = (httpContext, httpClient) =>
    {
        // Forward authenticated user's credentials to the feed
        var username = httpContext.User.Identity?.Name;
        var apiKey = httpContext.Items["NuGetApiKey"] as string;
        
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(apiKey))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{username}:{apiKey}"));
            httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);
        }
    };
});
```

**Key Benefits:**

- **Same-host default**: Zero configuration for integrated feed + UI scenarios
- **External feed support**: Connect UI to any NuGet v3 feed (nuget.org, Azure Artifacts, etc.)
- **Per-user authentication**: Verify user access rights and show only packages they're allowed to see
- **Flexible authentication**: Support API keys, tokens, or any authentication scheme

### Registering Custom Services

Inject custom metadata or search implementations:

```csharp
builder.Services.AddScoped<INuGetSearchService, CustomSearchService>();
builder.Services.AddScoped<IPackageMetadataService, CustomMetadataService>();
```

## Integration Examples

### Blazor Server App (Same-Host Feed)

When the NuGet API and UI are part of the **same application**:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register both the NuGet API and the Blazor UI
builder.Services.AddNuGetPackageApi(options => { /* feed config */ });
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// No configuration needed - auto-discovers from same host
builder.Services.AddNuGetSearchService();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapNuGetApiRoutes(); // NuGet API routes
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
```

### Blazor Server App (External Feed)

When connecting to a **separate NuGet feed host**:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Connect to external feed
builder.Services.AddNuGetSearchService(options =>
{
    options.ServiceIndexUrl = "https://packages.company.com/v3/index.json";
});

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
```

### Blazor Server with Authentication

For **private feeds** that require user authentication:

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add your authentication (ASP.NET Core Identity, Azure AD, etc.)
builder.Services.AddAuthentication(/* your auth config */);
builder.Services.AddAuthorization();

builder.Services.AddNuGetSearchService(options =>
{
    options.ServiceIndexUrl = "https://private-feed.company.com/v3/index.json";
    
    // Forward user credentials to the feed
    options.ConfigureHttpClient = (httpContext, httpClient) =>
    {
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            // Get user's API key from claims, database, or session
            var apiKey = user.FindFirst("NuGetApiKey")?.Value;
            if (!string.IsNullOrEmpty(apiKey))
            {
                httpClient.DefaultRequestHeaders.Add("X-NuGet-ApiKey", apiKey);
            }
        }
    };
});

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseStaticFiles();
app.UseRouting();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
```

```razor
@* Pages/Packages.razor *@
@page "/packages"
@using AvantiPoint.Packages.UI.Components

<PageTitle>Package Browser</PageTitle>

<h1>Browse Packages</h1>

<PackageSearch OnPackageSelected="@NavigateToPackage" />

@code {
    [Inject] private NavigationManager NavManager { get; set; } = default!;
    
    private void NavigateToPackage(SearchResult package)
    {
        NavManager.NavigateTo($"/packages/{package.Id}");
    }
}
```

### Razor Pages App

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(); // Required for components

// Auto-discovers from local feed
builder.Services.AddNuGetSearchService();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapBlazorHub(); // Required for components
app.Run();
```

```cshtml
@* Pages/Packages.cshtml *@
@page
@using AvantiPoint.Packages.UI.Components
@model PackagesModel

<h1>Internal Package Feed</h1>

<component type="typeof(PackageSearch)" 
           render-mode="ServerPrerendered" 
           param-Placeholder="@("Search internal packages...")"
           param-ResultsPerPage="30" />

@section Scripts {
    <script src="_framework/blazor.server.js"></script>
}
```

### MVC App with Custom Layout

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddServerSideBlazor();

// Auto-discovers from local feed
builder.Services.AddNuGetSearchService();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapBlazorHub();
app.Run();
```

```cshtml
@* Views/Packages/Index.cshtml *@
@using AvantiPoint.Packages.UI.Components

@{
    ViewData["Title"] = "Package Browser";
}

<div class="container">
    <h1>@ViewData["Title"]</h1>
    
    <component type="typeof(PackageSearch)" 
               render-mode="Server" />
</div>

@section Scripts {
    <script src="_framework/blazor.server.js"></script>
}
```

## Best Practices

### Performance

1. **Use Server Rendering:** For initial page load performance, use `ServerPrerendered` render mode
2. **Lazy Load Details:** Only load full package details when needed
3. **Cache Metadata:** Cache package metadata responses at the HTTP client level
4. **Paginate Results:** Keep `ResultsPerPage` reasonable (20-50 items)

### Accessibility

1. **Keyboard Navigation:** All interactive elements are keyboard accessible
2. **Screen Readers:** Components include proper ARIA labels and live regions
3. **Focus Management:** Focus is properly managed during modal interactions
4. **Color Contrast:** Default theme meets WCAG AA standards

### Customization

1. **Override CSS Variables:** Use CSS custom properties for theming
2. **Wrap Components:** Create wrapper components for app-specific defaults
3. **Event Handlers:** Use `OnPackageSelected` callbacks for custom navigation
4. **Service Injection:** Replace default services with custom implementations

## Troubleshooting

### Component Doesn't Render

**Problem:** Component appears as empty or shows as plain text.

**Solution:**
- Ensure `AddServerSideBlazor()` or `AddRazorComponents()` is called in `Program.cs`
- Add `MapBlazorHub()` to the middleware pipeline
- Include `<script src="_framework/blazor.server.js"></script>` in your layout

### Styles Not Applied

**Problem:** Components render but don't have proper styling.

**Solution:**
- Ensure `UseStaticFiles()` is called before routing
- Check that `_content/AvantiPoint.Packages.UI.Razor/` static assets are accessible
- Verify `<link>` tags are included (components add these automatically via `<HeadContent>`)

### Search Returns No Results

**Problem:** Search appears to work but returns 0 results.

**Solution:**
- Verify the feed's service index (`/v3/index.json`) is accessible
- Check that the search endpoint discovered from service index is accessible
- If using an external feed, ensure `ServiceIndexUrl` is correctly configured
- Ensure CORS is properly configured if feed is on different domain
- Check browser console for HTTP errors

### Readme Not Displaying

**Problem:** Package details show but readme section is empty.

**Solution:**
- Verify package actually has an embedded readme
- Check that `{baseUrl}/v3/package/{id}/{version}/readme` endpoint is accessible
- Ensure markdown rendering is working (check for JavaScript errors)

## API Reference

Full API documentation is available in the XML documentation comments included with the package. For IntelliSense support in Visual Studio or VS Code, these comments are automatically displayed during development.

## Examples Repository

Complete working examples are available in the [AvantiPoint.Packages repository](https://github.com/AvantiPoint/avantipoint.packages/tree/main/samples):

- **OpenFeed Sample:** Public feed without authentication
- **AuthenticatedFeed Sample:** Private feed with authentication

## Migration Guide

### From BaGet UI

If you're migrating from BaGet's built-in UI:

1. Replace `app.UsePackagesUI()` with component-based pages
2. Create Razor Pages or Blazor pages using `PackageSearch` and `PackageDetail`
3. Update routing to match your application's URL structure
4. Customize styling using CSS custom properties

## Contributing

The UI component library is open source and contributions are welcome:

- **Repository:** [github.com/AvantiPoint/avantipoint.packages](https://github.com/AvantiPoint/avantipoint.packages)
- **Issues:** Report bugs or request features via GitHub Issues
- **Pull Requests:** Submit PRs for bug fixes or new features

## License

AvantiPoint.Packages.UI.Razor is licensed under the MIT License.

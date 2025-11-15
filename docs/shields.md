# Package Badges (Shields)

AvantiPoint Packages includes built-in support for generating package version badges, similar to shields.io. This makes it easy to display package version information in your documentation, README files, or websites.

## Enabling Shields

To enable the shields endpoint, configure a server name in your `appsettings.json`:

```json
{
  "Shields": {
    "ServerName": "My Package Feed"
  }
}
```

If `ServerName` is null or empty, the shields endpoint is disabled and will return a 404.

## Using Badges

Once enabled, you can generate badges using the following URL pattern:

```
https://your-feed.example.com/api/shields/v/{packageId}.svg
```

### In Markdown

```markdown
![Package Version](https://your-feed.example.com/api/shields/v/MyPackage.svg)
```

Result: ![Package Version](https://img.shields.io/badge/MyPackage-1.0.0-blue.svg)

### In HTML

```html
<img src="https://your-feed.example.com/api/shields/v/MyPackage.svg" alt="Package Version" />
```

### In Documentation Sites

Most documentation generators (MkDocs, DocFX, etc.) support Markdown image syntax:

```markdown
## MyPackage ![Version](https://your-feed.example.com/api/shields/v/MyPackage.svg)

Current version: ![MyPackage Version](https://your-feed.example.com/api/shields/v/MyPackage.svg)
```

## Badge Styles

The shields use a simple, clean design that shows:
- Package name
- Latest version number
- The server name you configured

The color scheme is consistent with standard shields.io badges.

## Examples

### Single Package

Display the version of a specific package:

```markdown
### Newtonsoft.Json
![Version](https://your-feed.example.com/api/shields/v/Newtonsoft.Json.svg)
```

### Multiple Packages

Create a table of package versions:

```markdown
| Package | Version |
|---------|---------|
| MyCompany.Core | ![Version](https://your-feed.example.com/api/shields/v/MyCompany.Core.svg) |
| MyCompany.Web | ![Version](https://your-feed.example.com/api/shields/v/MyCompany.Web.svg) |
| MyCompany.Data | ![Version](https://your-feed.example.com/api/shields/v/MyCompany.Data.svg) |
```

### In GitHub README

```markdown
# My Project

![Build Status](https://github.com/mycompany/myproject/workflows/CI/badge.svg)
![Package Version](https://nuget.mycompany.com/api/shields/v/MyCompany.MyPackage.svg)

## Installation

```bash
dotnet add package MyCompany.MyPackage
```
```

## Comparison with NuGet.org Badges

For packages on NuGet.org, you would typically use shields.io:

```markdown
![NuGet](https://img.shields.io/nuget/v/Newtonsoft.Json.svg)
```

For packages on your private feed, use the built-in shields:

```markdown
![Private Feed](https://your-feed.example.com/api/shields/v/MyCompany.Package.svg)
```

## Authenticated Feeds

If your feed requires authentication, badges may not work in public documentation (like GitHub README files) because browsers won't send credentials.

**Solutions**:

1. **Public metadata** - Configure your feed to allow anonymous access to package metadata (but not downloads):
   ```csharp
   // In your IPackageAuthenticationService
   public Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
   {
       // Allow anonymous access for metadata only
       if (IsMetadataRequest())
       {
           return Task.FromResult(NuGetAuthenticationResult.Success(null));
       }
       
       // Require authentication for downloads
       // ... your authentication logic
   }
   ```

2. **Separate public metadata endpoint** - Deploy a separate, unauthenticated instance that only serves metadata

3. **Use in internal docs only** - Display badges only in internal documentation that users can authenticate to

## Customization

The shield style and colors are currently fixed. If you need custom styling, you have a few options:

1. **Use shields.io** - Generate custom badges with shields.io and point to your feed's metadata endpoint
2. **Fork and modify** - Modify the shield generation code in `AvantiPoint.Packages.Hosting`
3. **Proxy through shields.io** - Use shields.io's endpoint badge feature to proxy your feed

## Disabling Shields

To disable the shields endpoint, remove the `ServerName` configuration or set it to an empty string:

```json
{
  "Shields": {
    "ServerName": ""
  }
}
```

Or remove the section entirely:

```json
{
  // Shields section removed - endpoint disabled
}
```

## Troubleshooting

### Badge shows 404 or error

1. **Check configuration** - Ensure `ServerName` is set
2. **Check package exists** - Verify the package is in your feed
3. **Check URL** - Ensure the URL is correct (case-sensitive on Linux)

### Badge doesn't update

Badges are generated dynamically and should always show the latest version. If you're seeing stale data:

1. **Clear browser cache** - The image may be cached by your browser
2. **Check CDN cache** - If using a CDN, clear its cache
3. **Add cache-busting parameter** - Add a query parameter to force refresh:
   ```markdown
   ![Version](https://your-feed.example.com/api/shields/v/MyPackage.svg?v=2)
   ```

### Badge doesn't display in GitHub

GitHub caches images aggressively. To force an update:
1. Commit a change to your README
2. Add a cache-busting parameter to the URL
3. Wait a few minutes for GitHub's cache to update

## See Also

- [Configuration](configuration.md) - Configure AvantiPoint Packages
- [Getting Started](getting-started.md) - Set up your first feed
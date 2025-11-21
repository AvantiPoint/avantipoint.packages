---
id: download-tracking
title: Package Download Tracking
sidebar_label: Download Tracking
sidebar_position: 14
---

AvantiPoint Packages automatically tracks detailed information about every package download, providing comprehensive analytics for usage monitoring, security auditing, and business intelligence.

## Overview

Every time a package is downloaded, the system records:
- **User identity** - The authenticated username (if any)
- **IP address** - Remote client IP for geographic and security analysis
- **Client information** - NuGet client type and version
- **Platform details** - Operating system and version
- **Timestamp** - Exact date and time of download
- **User agent** - Full HTTP User-Agent string

This data is stored in the `PackageDownloads` table with a foreign key relationship to the `Packages` table.

## Data Schema

### PackageDownload Entity

```csharp
public class PackageDownload
{
    public Guid Id { get; set; }                    // Unique identifier
    public int PackageKey { get; set; }             // Reference to package
    public IPAddress RemoteIp { get; set; }         // Client IP address
    public string UserAgentString { get; set; }     // Full User-Agent header
    public string NuGetClient { get; set; }         // Client name (e.g., "NuGet Command Line")
    public string NuGetClientVersion { get; set; }  // Client version (e.g., "6.8.0")
    public string ClientPlatform { get; set; }      // OS (e.g., "Windows")
    public string ClientPlatformVersion { get; set; }// OS version (e.g., "10.0.22000")
    public string User { get; set; }                // Authenticated username
    public DateTimeOffset Timestamp { get; set; }   // Download timestamp (UTC)
}
```

## How It Works

### Automatic Tracking

Download tracking happens automatically in the `PackageService.AddDownloadAsync` method:

```csharp
public async Task<bool> AddDownloadAsync(string id, NuGetVersion version, CancellationToken cancellationToken)
{
    var package = await FindPackageAsync(id, version, cancellationToken);
    if (package == null) return false;

    // Parse User-Agent for client information
    var userAgent = _contextAccessor.HttpContext.Request.Headers["User-Agent"];
    var (client, version, platform, platformVersion) = ParseUserAgent(userAgent);

    // Get authenticated user
    var userName = _contextAccessor.HttpContext.User?.Identity?.Name;

    // Record download
    _context.PackageDownloads.Add(new PackageDownload
    {
        PackageKey = package.Key,
        RemoteIp = _contextAccessor.HttpContext.Connection.RemoteIpAddress,
        ClientPlatform = platform,
        ClientPlatformVersion = platformVersion,
        NuGetClient = client,
        NuGetClientVersion = version,
        User = userName,
        UserAgentString = userAgent,
    });

    return await _context.SaveChangesAsync(cancellationToken) > 0;
}
```

### User Agent Parsing

The system intelligently parses User-Agent strings to extract structured information:

**Example User-Agent strings:**

```
NuGet Command Line/6.8.0 (Microsoft Windows NT 10.0.22000.0)
→ Client: NuGet Command Line
→ Version: 6.8.0
→ Platform: Windows
→ Platform Version: 10.0.22000.0

dotnet/8.0.0 (win-x64)
→ Client: dotnet
→ Version: 8.0.0
→ Platform: Windows
→ Platform Version: (x64 architecture)

NuGet xplat/6.4.0 (Linux 5.15.0)
→ Client: NuGet xplat
→ Version: 6.4.0
→ Platform: Linux
→ Platform Version: 5.15.0
```

## Use Cases

### 1. Usage Analytics

Query download patterns to understand package adoption:

```sql
-- Most downloaded packages in the last 30 days
SELECT 
    p.Id,
    p.Version,
    COUNT(*) as Downloads
FROM PackageDownloads pd
INNER JOIN Packages p ON pd.PackageKey = p.[Key]
WHERE pd.Timestamp >= DATEADD(day, -30, GETUTCDATE())
GROUP BY p.Id, p.Version
ORDER BY Downloads DESC;
```

### 2. Security Auditing

Track downloads by IP address for security monitoring:

```sql
-- Detect unusual download patterns from single IP
SELECT 
    RemoteIp,
    COUNT(DISTINCT PackageKey) as UniquePackages,
    COUNT(*) as TotalDownloads
FROM PackageDownloads
WHERE Timestamp >= DATEADD(hour, -1, GETUTCDATE())
GROUP BY RemoteIp
HAVING COUNT(*) > 100
ORDER BY TotalDownloads DESC;
```

### 3. License Compliance

For commercial packages, track which users are downloading:

```sql
-- Downloads by user for licensing verification
SELECT 
    pd.User,
    p.Id as PackageId,
    COUNT(*) as DownloadCount,
    MAX(pd.Timestamp) as LastDownload
FROM PackageDownloads pd
INNER JOIN Packages p ON pd.PackageKey = p.[Key]
WHERE p.Id LIKE 'MyCompany.Premium%'
    AND pd.User IS NOT NULL
GROUP BY pd.User, p.Id
ORDER BY pd.User, DownloadCount DESC;
```

### 4. Client Version Analysis

Understand what tooling your users are running:

```sql
-- NuGet client version distribution
SELECT 
    NuGetClient,
    NuGetClientVersion,
    COUNT(*) as UsageCount
FROM PackageDownloads
WHERE Timestamp >= DATEADD(day, -7, GETUTCDATE())
    AND NuGetClient IS NOT NULL
GROUP BY NuGetClient, NuGetClientVersion
ORDER BY UsageCount DESC;
```

### 5. Platform Insights

Identify which platforms are consuming your packages:

```sql
-- Operating system distribution
SELECT 
    ClientPlatform,
    ClientPlatformVersion,
    COUNT(*) as Downloads
FROM PackageDownloads
WHERE Timestamp >= DATEADD(month, -1, GETUTCDATE())
    AND ClientPlatform IS NOT NULL
GROUP BY ClientPlatform, ClientPlatformVersion
ORDER BY Downloads DESC;
```

## Integration with Callbacks

Download tracking works seamlessly with event callbacks. Use `INuGetFeedActionHandler` to react to downloads:

```csharp
public class DownloadAnalyticsHandler : INuGetFeedActionHandler
{
    private readonly ILogger<DownloadAnalyticsHandler> _logger;
    private readonly IHttpContextAccessor _contextAccessor;

    public async Task OnPackageDownloaded(string packageId, string version)
    {
        var context = _contextAccessor.HttpContext;
        var ip = context.Connection.RemoteIpAddress;
        var user = context.User.Identity?.Name;
        var userAgent = context.Request.Headers["User-Agent"].ToString();

        _logger.LogInformation(
            "Package {PackageId} {Version} downloaded by {User} from {IP} using {UserAgent}",
            packageId, version, user ?? "anonymous", ip, userAgent);

        // Trigger custom business logic
        await SendToAnalyticsService(packageId, version, user, ip);
        await CheckLicenseCompliance(packageId, user);
        await UpdateUsageMetrics(packageId);
    }

    // Other handler methods...
}
```

## Database Views for Performance

AvantiPoint Packages includes pre-built views for efficient download analytics:

### vw_PackageDownloadCounts

Pre-aggregated download counts per package:

```sql
CREATE VIEW vw_PackageDownloadCounts AS
SELECT 
    p.[Key] as PackageKey,
    p.Id as PackageId,
    p.Version,
    COUNT(pd.Id) as DownloadCount
FROM Packages p
LEFT JOIN PackageDownloads pd ON p.[Key] = pd.PackageKey
GROUP BY p.[Key], p.Id, p.Version;
```

**Usage:**

```csharp
var topPackages = await context.Database
    .SqlQuery<PackageDownloadSummary>($@"
        SELECT TOP 10 PackageId, SUM(DownloadCount) as TotalDownloads
        FROM vw_PackageDownloadCounts
        GROUP BY PackageId
        ORDER BY TotalDownloads DESC")
    .ToListAsync();
```

## Privacy Considerations

### IP Address Storage

IP addresses are stored as `IPAddress` objects and can identify individual users. Consider:

- **Data retention policies** - Automatically purge old download records
- **Anonymization** - Hash or truncate IP addresses for privacy
- **Regional compliance** - Ensure GDPR/CCPA compliance if applicable

### User Information

The `User` field contains authenticated usernames. Ensure:

- Users are informed about download tracking in your terms of service
- Access to download data is restricted to authorized administrators
- Personal data is handled according to your privacy policy

## Configuration

### Disabling Download Tracking

Download tracking cannot be fully disabled, but you can:

1. **Limit data retention** - Periodically clean old records:

```sql
-- Delete download records older than 90 days
DELETE FROM PackageDownloads
WHERE Timestamp < DATEADD(day, -90, GETUTCDATE());
```

2. **Anonymize in real-time** - Override `AddDownloadAsync` in a custom service:

```csharp
public class AnonymizedPackageService : PackageService
{
    protected override async Task<bool> AddDownloadAsync(...)
    {
        // Store download without IP/User
        _context.PackageDownloads.Add(new PackageDownload
        {
            PackageKey = package.Key,
            RemoteIp = null,  // Omit IP
            User = null,      // Omit user
            // ... other fields
        });
        
        return await _context.SaveChangesAsync(cancellationToken) > 0;
    }
}
```

## Export and Reporting

### Export to CSV

```csharp
public async Task<string> ExportDownloadsToCsv(DateTime startDate, DateTime endDate)
{
    var downloads = await _context.PackageDownloads
        .Include(d => d.Package)
        .Where(d => d.Timestamp >= startDate && d.Timestamp <= endDate)
        .OrderByDescending(d => d.Timestamp)
        .ToListAsync();

    var csv = new StringBuilder();
    csv.AppendLine("Timestamp,Package,Version,User,IP,Client,Platform");
    
    foreach (var download in downloads)
    {
        csv.AppendLine($"{download.Timestamp:O},{download.Package.Id}," +
            $"{download.Package.Version},{download.User ?? "anonymous"}," +
            $"{download.RemoteIp},{download.NuGetClient}," +
            $"{download.ClientPlatform}");
    }
    
    return csv.ToString();
}
```

### Power BI Integration

Connect Power BI directly to your database for real-time dashboards:

1. Create a SQL view with the needed aggregations
2. Use DirectQuery or Import mode in Power BI
3. Build visualizations for:
   - Downloads over time
   - Top packages by downloads
   - Geographic distribution (if IP geolocation added)
   - Client version adoption

## Best Practices

1. **Index PackageKey** - Already indexed; critical for query performance
2. **Partition by date** - For large feeds, consider table partitioning on `Timestamp`
3. **Archive old data** - Move records older than 1 year to archive tables
4. **Monitor table growth** - Download tables can grow quickly on busy feeds
5. **Use views** - Leverage pre-built views instead of raw queries
6. **Batch deletes** - When purging old data, delete in batches to avoid locking

## Troubleshooting

### Download counts seem low

- Verify that `AddDownloadAsync` is being called
- Check for errors in application logs
- Ensure database migrations have run successfully
- Verify `PackageDownloads` table exists

### Missing user information

- User will be null for anonymous downloads
- Check that authentication is properly configured
- Verify `IHttpContextAccessor` is registered in DI

### IP address shows as null

- May occur behind certain proxies/load balancers
- Configure proxy headers: `app.UseForwardedHeaders()`
- Check `RemoteIpAddress` configuration

## See Also

- [Performance Optimization](performance-optimization.md) - Download aggregation views
- [Callbacks](callbacks.md) - React to download events
- [Authentication](authentication.md) - Track authenticated users
- [Database](database/index.md) - Database schema and migrations

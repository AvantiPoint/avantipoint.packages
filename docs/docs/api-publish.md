---
id: api-publish
title: Package Publish API
sidebar_label: Publish (Upload/Delete/Relist)
---

This page documents the package publishing lifecycle endpoints. AvantiPoint Packages maintains NuGet's legacy v2 surface for write operations while serving v3 for read/search.

## Read-Only Mode
If `PackageFeedOptions:IsReadOnlyMode=true` none of these endpoints are mapped.

## Upload Package
`PUT api/v2/package`

### Request
The NuGet client sends the raw `.nupkg` as the request body with `Content-Type: application/octet-stream`.

Example with `curl`:
```bash
curl -X PUT \
  -H "X-NuGet-ApiKey: YOUR_API_KEY" \
  --data-binary @MyLib.1.2.3.nupkg \
  https://feed.example.com/api/v2/package
```

### Responses
| Status | Meaning |
|--------|---------|
| 201 | Successfully indexed package |
| 400 | Invalid package stream |
| 410 | Duplicate metadata slot (package exists but content mismatch) |
| 500 | Internal indexing error |

### Notes
- API key is validated by `AuthorizedNuGetPublisherFilter` through `IPackageAuthenticationService`.
- On success internal callbacks (`INuGetFeedActionHandler`) for upload are invoked.

## Delete Package (Unlist)
`DELETE api/v2/package/{id}/{version}`

NuGet interprets this as an unlist operation.
```bash
curl -X DELETE \
  -H "X-NuGet-ApiKey: YOUR_API_KEY" \
  https://feed.example.com/api/v2/package/MyLib/1.2.3
```
| Status | Meaning |
|--------|---------|
| 204 | Package version unlisted |
| 404 | Package or version not found |

## Relist Package
`POST api/v2/package/{id}/{version}`
```bash
curl -X POST \
  -H "X-NuGet-ApiKey: YOUR_API_KEY" \
  https://feed.example.com/api/v2/package/MyLib/1.2.3
```
| Status | Meaning |
|--------|---------|
| 200 | Version re-listed |
| 404 | Package/version not found |

## Publisher Authentication Logic (Example)
```csharp
public async Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken ct)
{
    var token = await _db.ApiTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == apiKey, ct);
    if (token is null || token.IsRevoked || token.IsExpired) return NuGetAuthenticationResult.Fail("Invalid token", "MyFeed");

    if (!token.User.CanPublish) return NuGetAuthenticationResult.Fail("User cannot publish", "MyFeed");

    var identity = new ClaimsIdentity("NuGetPublisher");
    identity.AddClaim(new Claim(ClaimTypes.Name, token.User.UserName));
    return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
}
```

## Callback Hook Example
Implement `INuGetFeedActionHandler` to react to uploads:
```csharp
public class PublishEventHandler : INuGetFeedActionHandler
{
    public Task OnPackageUploadedAsync(string id, string version, ClaimsPrincipal user, CancellationToken ct)
    {
        // Queue indexing metrics, send notification, audit log, etc.
        return Task.CompletedTask;
    }
    // Other interface members omitted for brevity
}
```

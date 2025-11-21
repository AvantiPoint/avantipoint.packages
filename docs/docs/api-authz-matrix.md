---
id: api-authz-matrix
title: Endpoint Authorization Matrix
sidebar_label: Auth Matrix
---

This matrix maps endpoints to authentication/authorization requirements enforced by filters and `IPackageAuthenticationService`.

| Endpoint | Role Filter | Anonymous Allowed | Notes |
|----------|-------------|-------------------|-------|
| `v3/index.json` | None | Yes | Capability discovery |
| `v3/search` | `AuthorizedNuGetConsumerFilter` | Yes (filtered) | Consumer checks can restrict package visibility |
| `v3/autocomplete` | `AuthorizedNuGetConsumerFilter` | Yes | Same visibility rules as search |
| `v3/dependents` | `AuthorizedNuGetConsumerFilter` | Yes | Extension endpoint |
| `v3/registration*` | `AuthorizedNuGetConsumerFilter` | Yes | Metadata gated by consumer logic |
| `v3/package/{id}/index.json` | `AuthorizedNuGetConsumerFilter` | Yes | Version list may omit restricted packages |
| Download `.nupkg` | `AuthorizedNuGetConsumerFilter` + callback | Yes (if policy permits) | Callback may enforce license checks |
| Download `.nuspec` / `readme` / `icon` / `license` | `AuthorizedNuGetConsumerFilter` | Yes | Metadata only; can be exposed publicly |
| `PUT api/v2/package` | `AuthorizedNuGetPublisherFilter` | Technically yes (filter rejects) | Requires valid publisher token/API key |
| `DELETE api/v2/package/{id}/{version}` | `AuthorizedNuGetPublisherFilter` | Same | Unlist operation |
| `POST api/v2/package/{id}/{version}` | `AuthorizedNuGetPublisherFilter` | Same | Relist operation |
| `PUT api/v2/symbol` | `AuthorizedNuGetPublisherFilter` | Same | Symbol upload must match existing package |
| `GET api/download/symbols/*` | `AuthorizedNuGetConsumerFilter` | Yes | May restrict access based on package rights |
| `shield/{id}` / `shield/{id}/vpre` | None | Yes | Returns SVG; prerelease badge on `/vpre` |
| `v3/vulnerabilities/index.json` | None | Yes | Always present; may be empty |
| `v3/repository-signatures/index.json` | None | Yes | Certificate transparency |

## Auth Implementation Tips
- Differentiate consumer vs publisher logic in `IPackageAuthenticationService` methods.
- For public metadata exposure while protecting downloads, return `Success(null)` for consumer metadata requests but enforce checks in callbacks on package download.

## Callback Influence
`INuGetFeedActionHandler` can perform final authorization decisions (e.g., license entitlements) before completing downloads.

## Example: Mixed Public/Private Strategy
```csharp
public Task<NuGetAuthenticationResult> AuthenticateAsync(string user, string token, CancellationToken ct)
{
    if (IsMetadataRequest()) return Task.FromResult(NuGetAuthenticationResult.Success(null));
    // Validate user/token for download operations
    return ValidateConsumer(user, token, ct);
}
```

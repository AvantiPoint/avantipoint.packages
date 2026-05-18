---
id: api-overview
title: NuGet API Overview
sidebar_label: API Overview
sidebar_position: 1
---

This page provides a high-level overview of the NuGet-compatible HTTP APIs exposed by AvantiPoint Packages. These largely follow the official NuGet v3 protocol with a few extensions (Dependents, Shields, Vulnerability Index, Repository Signatures). All routes are relative to the feed base URL.

## Resources in Service Index (`v3/index.json`)
The service index advertises capability endpoints. Example (truncated):
```jsonc
{
  "version": "3.0.0",
  "resources": [
    { "@id": "https://feed.example.com/v3/package", "@type": "PackageBaseAddress/3.0.0" },
    { "@id": "https://feed.example.com/v3/search", "@type": "SearchQueryService/3.0.0" },
    { "@id": "https://feed.example.com/v3/autocomplete", "@type": "SearchAutocompleteService/3.0.0" },
    { "@id": "https://feed.example.com/v3/registration", "@type": "RegistrationsBaseUrl/3.6.0" },
    { "@id": "https://feed.example.com/v3/repository-signatures/index.json", "@type": "RepositorySignatures/4.9.0" },
    { "@id": "https://feed.example.com/v3/vulnerabilities/index.json", "@type": "VulnerabilityInfo/6.7.0" }
  ]
}
```

## Core Endpoint Groups
| Group | Purpose | Representative Paths |
|-------|---------|----------------------|
| Service Index | Capability discovery | `GET v3/index.json` |
| Search | Text/package search & autocomplete | `GET v3/search`, `GET v3/autocomplete` |
| Dependents (Extension) | Find packages that depend on a given ID | `GET v3/dependents?packageId=MyPackage` |
| Registration | Detailed metadata & version info | `GET v3/registration/{id}/index.json` / leaf variants |
| Package Content | Actual package/downloadable assets | `GET v3/package/{id}/index.json`, `.nupkg`, `.nuspec`, `readme`, `license`, `icon` |
| Publish | Upload, delete, relist packages (v2 legacy surface) | `PUT api/v2/package`, `DELETE api/v2/package/{id}/{version}`, `POST api/v2/package/{id}/{version}` |
| Symbols | Upload & fetch portable PDBs | `PUT api/v2/symbol`, `GET api/download/symbols/...` |
| Vulnerabilities | Surface vulnerability index pages | `GET v3/vulnerabilities/index.json` |
| Repository Signatures | Signing certificate info | `GET v3/repository-signatures/index.json` |
| Shields (Badges) | Version badge SVG | `GET shield/{id}`, `GET shield/{id}/vpre` |

## SemVer Behavior
- If `semVerLevel=2.0.0` is provided on registration & search endpoints SemVer2 packages are explicitly included.
- If omitted, implementation includes both SemVer1 and SemVer2 for backward compatibility.
- The `registration-gz-semver1` and `registration-gz-semver2` paths allow clients that pin older semantics to request specific sets.

## Read-Only Mode
When `PackageFeedOptions:IsReadOnlyMode=true` the following are disabled (not mapped):
- Package upload (`PUT api/v2/package`)
- Delete (`DELETE api/v2/package/{id}/{version}`)
- Relist (`POST api/v2/package/{id}/{version}`)
- Symbol upload (`PUT api/v2/symbol`)
All read endpoints continue to function.

## Custom Extensions
| Extension | Why It Exists | Notes |
|-----------|---------------|-------|
| Dependents | Internal analysis / impact tracking | Useful for determining downstream impact before unlisting. |
| Shields | Private feed package version badges | Returns SVG. Path differs from old docs: `shield/{id}` not `/api/shields/v/...`. |
| VulnerabilityInfo | Early adoption of NuGet protocol for security surfacing | Always present (may return empty pages if disabled). |
| RepositorySignatures | Enterprise signing transparency | Enumerates active signing certificates & fingerprints. |

## Caching & Compression Overview
Many GET endpoints apply internal caching (`UseNugetCaching`) to add appropriate HTTP cache headers (implementation-dependent). Gzip variants (`registration-gz-*`) apply compression filters for size optimization.

## Status Codes Summary (Condensed)
| Endpoint Category | Success | Not Found | Other |
|-------------------|---------|----------|-------|
| Search / Autocomplete | 200 | (N/A) Empty results | 400 (bad query) |
| Registration Index/Leaf | 200 | 404 | - |
| Package Content (versions/files) | 200 | 404 | - |
| Upload Package | 201 | 410 (stale content slot) / 400 | 500 (internal) |
| Delete Package | 204 | 404 | - |
| Relist Package | 200 | 404 | - |
| Symbol Upload | 201 | 404 (package missing) / 400 | 500 |
| Symbol Download | 200 | 404 | - |
| Shields | 200 | 200 ("Package not found" text in SVG) | - |
| Vulnerability Index | 200 | (Empty pages list) | - |
| Repository Signatures | 200 | (Empty cert list) | - |

## Next Steps
See individual API pages for detailed request/response bodies, curl examples, and configuration guidance.

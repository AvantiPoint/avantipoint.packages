---
id: api-registration
title: Registration (Metadata) API
sidebar_label: Registration
---

Provides rich metadata for packages and versions per NuGet v3 protocol.

## Index
`GET v3/registration/{id}/index.json`
- Optional `semVerLevel=2.0.0` to explicitly include SemVer2.
- If omitted this server includes both SemVer1 & SemVer2 for compatibility.

Gzip variants for legacy clients:
- SemVer1 only: `v3/registration-gz-semver1/{id}/index.json`
- SemVer2 capable: `v3/registration-gz-semver2/{id}/index.json`

## Leaf
`GET v3/registration/{id}/{version}.json`
Variants mirror index paths with `registration-gz-*` prefixes.

## Sample Index (Simplified)
```jsonc
{
  "count": 1,
  "items": [
    {
      "lower": "1.0.0",
      "upper": "1.0.0",
      "items": [
        {
          "catalogEntry": {
            "id": "MyLib",
            "version": "1.0.0",
            "authors": "Example",
            "description": "My library",
            "licenseExpression": "MIT",
            "packageTypes": [ { "name": "Dependency" } ]
          },
          "packageContent": "https://feed.example.com/v3/package/MyLib/1.0.0/MyLib.1.0.0.nupkg"
        }
      ]
    }
  ]
}
```

## Sample Leaf (Simplified)
```jsonc
{
  "catalogEntry": {
    "id": "MyLib",
    "version": "1.0.0",
    "summary": "Summary",
    "dependencyGroups": [
      {
        "targetFramework": "net8.0",
        "dependencies": [{ "id": "Newtonsoft.Json", "range": "[13.0.1,)" }]
      }
    ]
  },
  "packageContent": "https://feed.example.com/v3/package/MyLib/1.0.0/MyLib.1.0.0.nupkg"
}
```

## Status Codes
| 200 | 404 |
|-----|-----|
| JSON body | Not found (id or version) |

## Performance
- Endpoints leverage caching + conditional gzipping.
- Use `semVerLevel=2.0.0` only if client requires explicit SemVer2 filtering.

## Guidance
- Prefer Registration Index for rich metadata; use `v3/package/{id}/index.json` for lightweight version list.

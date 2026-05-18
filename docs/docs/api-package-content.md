---
id: api-package-content
title: Package Content API
sidebar_label: Package Content
---

Endpoints for fetching versions and downloadable assets.

## List Versions
`GET v3/package/{id}/index.json`
```bash
curl https://feed.example.com/v3/package/MyLib/index.json
```
Returns:
```jsonc
{ "versions": ["1.0.0","1.1.0","2.0.0-beta"] }
```
404 if package id unknown.

## Download Package (.nupkg)
`GET v3/package/{id}/{version}/{idVersion}.nupkg`
```bash
curl -LO https://feed.example.com/v3/package/MyLib/2.0.0/MyLib.2.0.0.nupkg
```

## Download Manifest (.nuspec)
`GET v3/package/{id}/{version}/{id2}.nuspec`
```bash
curl -LO https://feed.example.com/v3/package/MyLib/2.0.0/MyLib.nuspec
```

## Download ReadMe
`GET v3/package/{id}/{version}/readme`
Outputs markdown.
```bash
curl https://feed.example.com/v3/package/MyLib/2.0.0/readme -o ReadMe.md
```

## Download Icon
`GET v3/package/{id}/{version}/icon`
Return image stream. Content type may vary by embedded icon.

## Download License
`GET v3/package/{id}/{version}/license`
Plain text or license file extracted from package.

## Status Codes Summary
| Endpoint | 200 | 404 |
|----------|-----|-----|
| Versions | Versions list | Unknown id |
| .nupkg | File stream | Unknown id/version |
| .nuspec | File stream | Unknown id/version |
| ReadMe | Markdown file | Unknown id/version |
| Icon | Image | Unknown id/version |
| License | Text | Unknown id/version |

## SemVer Parsing
Version must parse with `NuGetVersion`; invalid formats yield 404.

## Callbacks
Package download triggers `HandlePackageDownloadedFilter` enabling business logic (metrics, license checks, throttling).

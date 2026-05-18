---
id: api-vulnerabilities
title: Vulnerability Info API
sidebar_label: Vulnerabilities
---

The Vulnerability Info resource advertises pages of package vulnerability data. Endpoint:
`GET v3/vulnerabilities/index.json`

### Behavior
- Always mapped to avoid client warnings.
- If `EnableVulnerabilityInfo=false` returns an empty `pages` array.

### Sample Enabled Response
```jsonc
{
  "version": "6.7.0",
  "pages": [
    {
      "@id": "vulnerabilities-base.json",
      "@name": "base",
      "@updated": "2025-11-19T12:00:00Z"
    }
  ]
}
```
### Sample Disabled Response
```jsonc
{ "version": "6.7.0", "pages": [] }
```

### Configuration
`appsettings.json`:
```json
{
  "EnableVulnerabilityInfo": true
}
```
Environment variable:
```bash
export EnableVulnerabilityInfo=true
```

### Data Population
Implement `IVulnerabilityService` to return latest update timestamp. Future expansion may include paging multiple files (e.g., severity partitions).

### Use Cases
- IDEs and clients surface warnings for vulnerable packages.
- Security dashboards aggregate counts.

If you disable support, ensure internal tooling does not rely on these pages.

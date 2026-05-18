---
id: api-symbols
title: Symbols API
sidebar_label: Symbols
---

AvantiPoint Packages supports publishing and serving portable PDB symbol packages (`.snupkg`). This enables source-level debugging by IDEs.

## Upload Symbols
`PUT api/v2/symbol`
```bash
curl -X PUT \
  -H "X-NuGet-ApiKey: YOUR_API_KEY" \
  --data-binary @MyLib.1.2.3.snupkg \
  https://feed.example.com/api/v2/symbol
```
| Status | Meaning |
|--------|---------|
| 201 | Symbols accepted & indexed |
| 400 | Invalid symbol package |
| 404 | Referenced `.nupkg` package not found |
| 500 | Internal error |

On success a `HandleSymbolsUploadedFilter` triggers callbacks.

## Download Symbols
Symbols are downloaded using hashed file paths produced by debugger requests:
`GET api/download/symbols/{file}/{key}/{file2}` or with prefix: `GET api/download/symbols/{prefix}/{file}/{key}/{file2}`

Example (simulated):
```bash
curl -LO "https://feed.example.com/api/download/symbols/MyLib.pdb/ABC123DEF456/MyLib.pdb"
```
| Status | Meaning |
|--------|---------|
| 200 | PDB file stream |
| 404 | Not found |

## Callback Example (Download)
```csharp
public class SymbolEvents : INuGetFeedActionHandler
{
    public Task OnSymbolsDownloadedAsync(string packageId, string version, string fileName, ClaimsPrincipal user, CancellationToken ct)
    {
        // Increment metrics, trace usage, security monitoring
        return Task.CompletedTask;
    }
}
```

## Notes
- Symbol uploads disabled when repository is read-only.
- Only portable PDBs inside `.snupkg` are supported.
- Keep `.snupkg` versions aligned with `.nupkg` versions.

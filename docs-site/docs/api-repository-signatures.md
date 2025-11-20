---
id: api-repository-signatures
title: Repository Signatures API
sidebar_label: Repository Signatures
---

The Repository Signatures resource surfaces active signing certificates used to repository-sign packages. Endpoint:
`GET v3/repository-signatures/index.json`

### Sample Response
```jsonc
{
  "allRepositorySigned": true,
  "signingCertificates": [
    {
      "fingerprints": {
        "2.16.840.1.101.3.4.2.1": "a1b2c3...sha256",
        "2.16.840.1.101.3.4.2.2": "d4e5f6...sha384",
        "2.16.840.1.101.3.4.2.3": "f00baa...sha512"
      },
      "subject": "CN=Repo Signing Cert, O=Example Corp, C=US",
      "issuer": "CN=Example CA, O=Example Corp, C=US",
      "notBefore": "2025-01-01T00:00:00Z",
      "notAfter": "2026-01-01T00:00:00Z",
      "contentUrl": "https://feed.example.com/certs/repoSigning.crt"
    }
  ]
}
```
- OID keys map to hash algorithms (SHA-256, SHA-384, SHA-512).
- `allRepositorySigned` indicates whether every package is repository signed.

### Populating Certificates
Certificates are provided by the `RepositorySigningCertificateService`. Typical workflow:
1. Import certificate metadata (fingerprints, subject, issuer) into your data store.
2. Set `ContentUrl` if hosting downloadable `.crt`.
3. Mark old certificates inactive so they drop off the list.

### Verifying Fingerprints
Example PowerShell to get SHA-256 fingerprint:
```powershell
Get-FileHash .\repoSigning.cer -Algorithm SHA256 | Select-Object -ExpandProperty Hash | ForEach-Object { $_.ToLower() }
```

### Use Cases
- Client tooling can verify trust anchors.
- Internal audits: prove signing continuity and expiry schedules.

If no certificates are active response contains `"signingCertificates": []` and `allRepositorySigned=false`.

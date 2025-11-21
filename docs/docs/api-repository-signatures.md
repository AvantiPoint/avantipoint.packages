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
        "2.16.840.1.101.3.4.2.1": "a1b2c3d4e5f6..."
      },
      "subject": "CN=My Repository Signer, O=Example Corp, C=US",
      "issuer": "CN=My Repository Signer, O=Example Corp, C=US",
      "notBefore": "2025-01-01T00:00:00Z",
      "notAfter": "2026-01-01T00:00:00Z",
      "contentUrl": "https://feed.example.com/v3/certificates/a1b2c3d4e5f6....crt"
    }
  ]
}
```

**Response Fields:**
- `allRepositorySigned` - Indicates whether every package is repository signed
- `signingCertificates` - Array of active signing certificates
  - `fingerprints` - Hash algorithm OID to fingerprint mapping (currently SHA-256 only)
  - `subject` - Certificate subject name
  - `issuer` - Certificate issuer name
  - `notBefore` - Certificate validity start date
  - `notAfter` - Certificate validity end date
  - `contentUrl` - Optional URL to download the public certificate file

**Note:** The implementation currently uses SHA-256 fingerprints only. The OID `2.16.840.1.101.3.4.2.1` corresponds to SHA-256.

### Automatic Certificate Tracking

Certificates are automatically tracked by the `RepositorySigningCertificateService`:
- Certificates are recorded when first used for signing
- Certificate metadata (fingerprints, subject, issuer) is stored in the database
- Public certificate bytes are stored for download
- `ContentUrl` is automatically generated for certificate downloads
- Certificates remain in the database even if the certificate file is deleted
- Old certificates can be marked inactive to remove them from the response

### Verifying Fingerprints
Example PowerShell to get SHA-256 fingerprint:
```powershell
Get-FileHash .\repoSigning.cer -Algorithm SHA256 | Select-Object -ExpandProperty Hash | ForEach-Object { $_.ToLower() }
```

### Use Cases
- Client tooling can verify trust anchors.
- Internal audits: prove signing continuity and expiry schedules.

If no certificates are active response contains `"signingCertificates": []` and `allRepositorySigned=false`.

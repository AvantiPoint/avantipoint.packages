---
id: signing
title: Repository Package Signing
sidebar_label: Package Signing
sidebar_position: 8
---

AvantiPoint Packages supports repository-level package signing, allowing you to cryptographically sign all packages in your feed. This provides integrity verification and trust for clients consuming your packages.

## Overview

Repository signing adds a **repository signature** to each package, which indicates that the package came from a trusted repository. This is different from **author signatures**, which indicate the package came from a trusted publisher.

### Key Features

- **Self-signed certificates** - Automatically generate and manage certificates
- **Stored certificates** - Use existing certificates from files or certificate stores
- **Cloud Key Vault / Managed HSM** - Use certificates from Azure Key Vault, AWS KMS/Signer, or Google Cloud KMS/HSM (FIPS 140-2 Level 2/3 validated)
- **Automatic signing** - Packages are signed during upload
- **On-demand signing** - Unsigned packages are signed when first downloaded
- **Timestamping** - Signatures remain valid after certificate expiration
- **Certificate tracking** - All certificates are tracked in the database
- **Upstream signature handling** - Configurable behavior for packages with existing signatures

## Configuration

Repository signing is configured in `appsettings.json` under the `Signing` section:

```json
{
  "Signing": {
    "Mode": "SelfSigned",
    "CertificatePasswordSecret": "Signing:CertificatePassword",
    "TimestampServerUrl": "http://timestamp.digicert.com",
    "UpstreamSignature": "ReSign",
    "SelfSigned": {
      "SubjectName": "CN=My Repository Signer, O=MyOrg, C=US",
      "KeySize": "KeySize4096",
      "ValidityInDays": 3650,
      "CertificatePath": "certs/repository-signing.pfx"
    }
  }
}
```

### Signing Modes

#### Self-Signed Mode

Generate and use a self-signed certificate automatically:

```json
{
  "Signing": {
    "Mode": "SelfSigned",
    "SelfSigned": {
      "SubjectName": "CN=My Repository Signer",
      "Organization": "MyOrg",
      "OrganizationalUnit": "IT",
      "Country": "US",
      "KeySize": "KeySize4096",
      "ValidityInDays": 3650,
      "CertificatePath": "certs/repository-signing.pfx"
    }
  }
}
```

**Properties:**
- `SubjectName` (optional) - Complete subject name. If not provided, constructed from `Organization`, `OrganizationalUnit`, `Country`, and `Shields.ServerName`
- `Organization` (optional) - Organization (O) component
- `OrganizationalUnit` (optional) - Organizational Unit (OU) component
- `Country` (optional) - 2-letter ISO country code
- `KeySize` - RSA key size: `KeySize2048`, `KeySize3072`, or `KeySize4096` (default: `KeySize4096`)
- `ValidityInDays` - Certificate validity period in days (default: 3650, max: 3650)
- `CertificatePath` - Path in storage where PFX is saved (default: `certs/repository-signing.pfx`)
- `HashAlgorithm` - Hash algorithm for signing (default: `SHA256`)

**Behavior:**
- Certificate is generated on first use
- Certificate is persisted to storage at `CertificatePath`
- Certificate is reused if it matches the current configuration
- New certificate is generated if configuration changes or certificate expires
- Certificate usage is automatically tracked in the database

#### Stored Certificate Mode

Use an existing certificate from a file or certificate store:

```json
{
  "Signing": {
    "Mode": "StoredCertificate",
    "CertificatePasswordSecret": "Signing:CertificatePassword",
    "StoredCertificate": {
      "FilePath": "certs/repository-signing.pfx",
      "Password": "optional-password-if-not-using-secret"
    }
  }
}
```

**From Certificate Store:**

```json
{
  "Signing": {
    "Mode": "StoredCertificate",
    "StoredCertificate": {
      "Thumbprint": "a1b2c3d4e5f6...",
      "StoreName": "My",
      "StoreLocation": "LocalMachine"
    }
  }
}
```

**Properties:**
- `FilePath` - Path to PFX/P12 certificate file (required for file-based)
- `Thumbprint` - SHA1 thumbprint (required for store-based)
- `StoreName` - Certificate store name: `My`, `Root`, `TrustedPeople`, etc. (required for store-based)
- `StoreLocation` - Store location: `CurrentUser` or `LocalMachine` (required for store-based)
- `Password` - Certificate password (optional if using `CertificatePasswordSecret`)

**Behavior:**
- Certificate is validated on startup
- Package uploads fail if certificate is expired or expiring within 5 minutes
- Package uploads fail if signing fails (prevents unsigned packages)

#### Azure Key Vault Mode

Use a certificate stored in Azure Key Vault (Premium tier with HSM-backed keys):

**First, install the package:**
```bash
dotnet add package AvantiPoint.Packages.Signing.Azure
```

**Then configure in `Program.cs`:**
```csharp
using AvantiPoint.Packages.Signing.Azure;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddRepositorySigning();
    options.AddAzureKeyVaultSigning(); // Enable Azure Key Vault support
});
```

**Configuration:**
```json
{
  "Signing": {
    "Mode": "AzureKeyVault",
    "AzureKeyVault": {
      "VaultUri": "https://myvault.vault.azure.net/",
      "CertificateName": "repository-signing-cert",
      "CertificateVersion": null,
      "AuthenticationMode": "Default",
      "TenantId": null,
      "ClientId": null,
      "ClientSecretConfigurationKey": "Azure:KeyVault:ClientSecret"
    }
  }
}
```

**Properties:**
- `VaultUri` (required) - Azure Key Vault URI
- `CertificateName` (required) - Name of the certificate in Key Vault
- `CertificateVersion` (optional) - Specific version to use, or null for latest
- `AuthenticationMode` - `Default` (uses DefaultAzureCredential), `ManagedIdentity`, or `ClientSecret`
- `TenantId` - Required when `AuthenticationMode` is `ClientSecret`
- `ClientId` - Required when `AuthenticationMode` is `ClientSecret`
- `ClientSecret` or `ClientSecretConfigurationKey` - Required when `AuthenticationMode` is `ClientSecret`

**Note:** For HSM-backed non-exportable certificates, the certificate must be marked as exportable, or a custom signing implementation using Key Vault's signing operations would be required.

#### AWS KMS Mode

Use AWS Key Management Service (KMS) with HSM-backed keys:

**First, install the package:**
```bash
dotnet add package AvantiPoint.Packages.Signing.Aws
```

**Then configure in `Program.cs`:**
```csharp
using AvantiPoint.Packages.Signing.Aws;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddRepositorySigning();
    options.AddAwsKmsSigning(); // Enable AWS KMS support
});
```

**Configuration:**
```json
{
  "Signing": {
    "Mode": "AwsKms",
    "AwsKms": {
      "Region": "us-east-1",
      "KeyId": "arn:aws:kms:us-east-1:123456789012:key/12345678-1234-1234-1234-123456789012",
      "SigningAlgorithm": "RSASSA_PSS_SHA_256",
      "AccessKeyId": null,
      "SecretAccessKeyConfigurationKey": "AWS:SecretAccessKey"
    }
  }
}
```

**Properties:**
- `Region` (required) - AWS region (e.g., us-east-1, us-west-2)
- `KeyId` (required) - KMS key ID or ARN
- `SigningAlgorithm` - Signing algorithm (default: RSASSA_PSS_SHA_256)
- `AccessKeyId` (optional) - AWS access key ID, or null to use default credential chain
- `SecretAccessKey` or `SecretAccessKeyConfigurationKey` (optional) - AWS secret access key

**Note:** AWS KMS does not export private keys. This mode currently requires a custom signing implementation using KMS Sign API.

#### AWS Signer Mode

Use AWS Signer managed code signing service:

**First, install the package:**
```bash
dotnet add package AvantiPoint.Packages.Signing.Aws
```

**Then configure in `Program.cs`:**
```csharp
using AvantiPoint.Packages.Signing.Aws;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddRepositorySigning();
    options.AddAwsSignerSigning(); // Enable AWS Signer support
});
```

**Configuration:**
```json
{
  "Signing": {
    "Mode": "AwsSigner",
    "AwsSigner": {
      "Region": "us-east-1",
      "ProfileName": "my-signing-profile",
      "AccessKeyId": null,
      "SecretAccessKeyConfigurationKey": "AWS:SecretAccessKey"
    }
  }
}
```

**Properties:**
- `Region` (required) - AWS region
- `ProfileName` (required) - Signing profile name in AWS Signer
- `AccessKeyId` (optional) - AWS access key ID, or null to use default credential chain
- `SecretAccessKey` or `SecretAccessKeyConfigurationKey` (optional) - AWS secret access key

**Note:** AWS Signer manages certificates internally. This mode currently requires a custom signing implementation using Signer's StartSigningJob API.

#### Google Cloud KMS Mode

Use Google Cloud Key Management Service (KMS) with HSM protection level:

**First, install the package:**
```bash
dotnet add package AvantiPoint.Packages.Signing.Gcp
```

**Then configure in `Program.cs`:**
```csharp
using AvantiPoint.Packages.Signing.Gcp;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddRepositorySigning();
    options.AddGcpKmsSigning(); // Enable GCP KMS support
});
```

**Configuration:**
```json
{
  "Signing": {
    "Mode": "GcpKms",
    "GcpKms": {
      "ProjectId": "my-project",
      "Location": "us-east1",
      "KeyRing": "my-key-ring",
      "KeyName": "repository-signing-key",
      "KeyVersion": null,
      "ProtectionLevel": "Hsm",
      "ServiceAccountKeyPathConfigurationKey": "GCP:ServiceAccountKeyPath"
    }
  }
}
```

**Properties:**
- `ProjectId` (required) - GCP project ID
- `Location` (required) - Key ring location (e.g., us-east1, global)
- `KeyRing` (required) - Key ring name
- `KeyName` (required) - Crypto key name
- `KeyVersion` (optional) - Specific key version, or null for primary version
- `ProtectionLevel` - `Software` or `Hsm` (default: Hsm)
- `ServiceAccountKeyPath` or `ServiceAccountKeyPathConfigurationKey` (optional) - Path to service account JSON key file, or null to use Application Default Credentials

**Note:** GCP KMS does not export private keys. This mode currently requires a custom signing implementation using KMS AsymmetricSign API.

#### Google Cloud HSM Mode

Use Google Cloud HSM (fully managed HSM service):

**First, install the package:**
```bash
dotnet add package AvantiPoint.Packages.Signing.Gcp
```

**Then configure in `Program.cs`:**
```csharp
using AvantiPoint.Packages.Signing.Gcp;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddRepositorySigning();
    options.AddGcpHsmSigning(); // Enable GCP HSM support
});
```

**Configuration:**
```json
{
  "Signing": {
    "Mode": "GcpHsm",
    "GcpHsm": {
      "ProjectId": "my-project",
      "Location": "us-east1",
      "ClusterName": "my-hsm-cluster",
      "ServiceAccountKeyPathConfigurationKey": "GCP:ServiceAccountKeyPath"
    }
  }
}
```

**Properties:**
- `ProjectId` (required) - GCP project ID
- `Location` (required) - HSM cluster location
- `ClusterName` (required) - HSM cluster name
- `ServiceAccountKeyPath` or `ServiceAccountKeyPathConfigurationKey` (optional) - Path to service account JSON key file, or null to use Application Default Credentials

**Note:** GCP HSM integration requires additional setup. This mode currently requires a custom signing implementation.

### Certificate Password

The certificate password can be configured in two ways:

1. **Top-level secret** (recommended):
   ```json
   {
     "Signing": {
       "CertificatePasswordSecret": "Signing:CertificatePassword"
     }
   }
   ```
   Then set the actual password via environment variable or configuration:
   ```bash
   export Signing__CertificatePassword="my-secure-password"
   ```

2. **In StoredCertificate options** (less secure):
   ```json
   {
     "Signing": {
       "StoredCertificate": {
         "Password": "my-password"
       }
     }
   }
   ```

The top-level `CertificatePasswordSecret` takes precedence if both are configured.

### Timestamping

Signatures are timestamped by default to ensure they remain valid after certificate expiration:

```json
{
  "Signing": {
    "TimestampServerUrl": "http://timestamp.digicert.com"
  }
}
```

- **Default**: DigiCert timestamp server (`http://timestamp.digicert.com`)
- **Custom**: Set `TimestampServerUrl` to any RFC 3161 timestamp server
- **Disable**: Set to empty string `""` (not recommended - signatures become invalid when certificate expires)

### Upstream Signature Handling

When packages are uploaded that already have repository signatures (e.g., downloaded from nuget.org), you can configure the behavior:

```json
{
  "Signing": {
    "UpstreamSignature": "ReSign"
  }
}
```

**Options:**
- `ReSign` (default) - Strip existing repository signature and replace with our own. Author signatures are preserved.
- `Reject` - Reject package uploads that already have repository signatures

**Example - Strict Mode:**

```json
{
  "Signing": {
    "Mode": "SelfSigned",
    "UpstreamSignature": "Reject",
    "SelfSigned": {
      "SubjectName": "CN=My Repository Signer"
    }
  }
}
```

## Program.cs Setup

Repository signing is automatically enabled when you configure it. For cloud provider modes, you must also call the corresponding extension method:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
    options.AddSqliteDatabase("Sqlite");
    options.AddRepositorySigning();
    
    // For cloud provider modes, add the corresponding package and call the extension method:
    // options.AddAzureKeyVaultSigning();      // Requires AvantiPoint.Packages.Signing.Azure
    // options.AddAwsKmsSigning();              // Requires AvantiPoint.Packages.Signing.Aws
    // options.AddAwsSignerSigning();           // Requires AvantiPoint.Packages.Signing.Aws
    // options.AddGcpKmsSigning();             // Requires AvantiPoint.Packages.Signing.Gcp
    // options.AddGcpHsmSigning();             // Requires AvantiPoint.Packages.Signing.Gcp
});
```

## How It Works

### During Package Upload

1. Package is uploaded via `/api/v2/package`
2. If signing is enabled:
   - Package is validated
   - If package has existing repository signature:
     - If `UpstreamSignature = ReSign`: Existing signature is stripped (author signatures preserved)
     - If `UpstreamSignature = Reject`: Upload fails with error
   - Package is signed with repository signature
   - Signed package is saved to storage
   - Certificate usage is recorded in database

### During Package Download

1. Client requests package via `/v3/package/{id}/{version}/{id}.{version}.nupkg`
2. If signing is enabled:
   - System checks for pre-signed package
   - If not found:
     - If package has existing repository signature:
       - If `UpstreamSignature = ReSign`: Signature is stripped and package is re-signed
       - If `UpstreamSignature = Reject`: Unsigned package is served (with warning)
     - Package is signed on-demand
     - Signed package is saved asynchronously for future downloads

### Certificate Tracking

All certificates used for signing are automatically tracked in the database:

- Certificate fingerprints (SHA-256)
- Subject and issuer information
- Validity period
- First and last usage dates
- Active/inactive status
- Public certificate bytes (for download)

Certificates remain in the database even if the certificate file is deleted, allowing clients to verify packages signed with old certificates.

## API Endpoints

### Repository Signatures

**Endpoint:** `GET /v3/repository-signatures/index.json`

Returns all active signing certificates:

```json
{
  "allRepositorySigned": true,
  "signingCertificates": [
    {
      "fingerprints": {
        "2.16.840.1.101.3.4.2.1": "a1b2c3d4e5f6..."
      },
      "subject": "CN=My Repository Signer",
      "issuer": "CN=My Repository Signer",
      "notBefore": "2025-01-01T00:00:00Z",
      "notAfter": "2026-01-01T00:00:00Z",
      "contentUrl": "https://feed.example.com/v3/certificates/a1b2c3d4e5f6....crt"
    }
  ]
}
```

### Certificate Download

**Endpoint:** `GET /v3/certificates/{fingerprint}.crt`

Downloads the public certificate file in DER format.

## Certificate Rotation

### Self-Signed Certificates

Self-signed certificates automatically rotate when:
- Certificate expires
- Configuration changes (subject, key size, etc.)
- Certificate is manually deleted from storage

The new certificate is automatically generated and used. Old certificates remain in the database for verification of previously signed packages.

### Stored Certificates

For stored certificates, you must:
1. Update the certificate file or certificate store
2. Restart the application
3. The new certificate will be used for new signings
4. Old certificates remain in the database for verification

### Cloud Key Vault Certificates

For cloud key vault certificates:
1. Update the certificate in the cloud key vault (Azure Key Vault, AWS KMS, or GCP KMS)
2. Restart the application
3. The new certificate will be retrieved and used for new signings
4. Old certificates remain in the database for verification

**Note:** For Azure Key Vault, you can specify a specific `CertificateVersion` to pin to a particular version, or leave it null to always use the latest version.

## Best Practices

1. **Use stored certificates in production** - Self-signed certificates are suitable for development/testing only
2. **Enable timestamping** - Ensures signatures remain valid after certificate expiration
3. **Store passwords securely** - Use `CertificatePasswordSecret` with environment variables or secret stores
4. **Monitor certificate expiration** - Set up alerts for certificates expiring within 30 days
5. **Use strong key sizes** - Prefer `KeySize4096` for production
6. **Configure upstream signature behavior** - Choose `ReSign` for flexibility or `Reject` for strict control

## Troubleshooting

### Certificate Not Found

**Error:** "Certificate not found" or "Certificate file does not exist"

**Solution:**
- For self-signed: Certificate will be generated on first use
- For stored: Verify `FilePath` is correct or `Thumbprint` exists in the certificate store

### Certificate Expired

**Error:** "Certificate is expired" or "Certificate expires within 5 minutes"

**Solution:**
- For self-signed: Certificate will be automatically regenerated
- For stored: Update certificate file/store and restart application
- For cloud providers: Update the certificate in the cloud key vault and restart application
- For cloud providers: Update the certificate in the cloud key vault and restart application

### Signing Fails

**Error:** "Failed to sign package"

**Solution:**
- Verify certificate has a private key
- Check certificate password is correct
- Ensure certificate is not expired
- For self-signed: Package upload still succeeds (graceful fallback)
- For stored: Package upload fails (prevents unsigned packages)

### Package Already Has Repository Signature

**Error:** "Package already has a repository signature"

**Solution:**
- Set `UpstreamSignature: "ReSign"` to strip and re-sign (default)
- Or set `UpstreamSignature: "Reject"` to reject such packages

## Cloud Provider Packages

Cloud provider signing support is provided through separate NuGet packages:

- **`AvantiPoint.Packages.Signing.Azure`** - Azure Key Vault support
- **`AvantiPoint.Packages.Signing.Aws`** - AWS KMS and Signer support
- **`AvantiPoint.Packages.Signing.Gcp`** - Google Cloud KMS and HSM support

These packages are optional - only install the packages for the cloud providers you use. This keeps your application lightweight and avoids unnecessary dependencies.

## See Also

- [Repository Signatures API](api-repository-signatures.md) - API endpoint documentation
- [Configuration](configuration.md) - General configuration guide


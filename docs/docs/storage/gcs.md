---
id: gcs
title: Google Cloud Storage
sidebar_label: Google Cloud Storage
---

Store packages in Google Cloud Storage (GCS) buckets.

## Package

```bash
dotnet add package AvantiPoint.Packages.Gcp
```

## Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "Gcs",
    "Bucket": "my-nuget-packages",
    "Prefix": "packages",
    "CredentialsPath": "/path/to/service-account.json"
  }
}
```

`Type` may also be `GoogleCloudStorage`.

**Program.cs**:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddGcsStorage();
});
```

Or use auto-discovery:

```csharp
options.AutoDiscoverGcsStorage();
```

## Authentication

- **Production:** Set `CredentialsPath` to a service account JSON key, or rely on [Application Default Credentials](https://cloud.google.com/docs/authentication/application-default-credentials) (`GOOGLE_APPLICATION_CREDENTIALS`).
- **Signed download URLs:** `GetDownloadUriAsync` returns V4 signed URLs (similar to S3 presigned URLs and Azure SAS). Not available when using the emulator.

## Emulator (development / CI)

For local testing without a GCP project, use [fake-gcs-server](https://github.com/fsouza/fake-gcs-server):

```json
{
  "Storage": {
    "Type": "Gcs",
    "Bucket": "packages",
    "EmulatorHost": "http://localhost:4443",
    "UseEmulator": true
  }
}
```

### Local testing

```bash
docker run -p 4443:4443 fsouza/fake-gcs-server -scheme http
```

Integration tests in `AvantiPoint.Packages.Storage.Tests` use Testcontainers with the same image and configure `/_internal/config` for the mapped host port.

## Related

- [AWS S3](awss3.md)
- [Azure Blob Storage](azureblob.md)
- [S3-Compatible providers](s3-compatible.md)

---
id: minio
title: MinIO (S3-Compatible)
sidebar_label: MinIO
---

MinIO is a high-performance, S3-compatible object storage server that you can run locally or in your own infrastructure.

This page shows how to configure AvantiPoint.Packages to use MinIO via the existing `AwsS3` storage provider.

## When to use MinIO

Use MinIO when you:

- Want S3-compatible storage without depending on AWS
- Need a fast local or on-premises object store
- Want a lightweight S3 emulator for development and testing

## Configuration

MinIO speaks the S3 API, so you configure it via the `AwsS3` storage provider using `ServiceUrl` and `ForcePathStyle`.

### Example `appsettings.json`

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-east-1",
    "Bucket": "packages",
    "AccessKey": "minioadmin",
    "SecretKey": "minioadmin",
    "ServiceUrl": "http://localhost:9000",
    "ForcePathStyle": true
  }
}
```

### Notes

- **ServiceUrl**: Your MinIO endpoint (default for Docker/dev is usually `http://localhost:9000`).
- **ForcePathStyle**: Must be `true` for MinIO so requests use `http://host/bucket/key` style URLs.
- **Region**: MinIO is typically configured with `us-east-1`, but you can match whatever you configured.

## Running MinIO locally

See the MinIO docs for install/run options, but a typical Docker command looks like:

```bash
docker run -p 9000:9000 -p 9001:9001 \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin \
  minio/minio server /data --console-address ":9001"
```

Then create a bucket called `packages` via the MinIO console (`http://localhost:9001`) or the `mc` CLI.

## Official documentation

- MinIO Docs: https://min.io/docs/
- MinIO GitHub: https://github.com/minio/minio

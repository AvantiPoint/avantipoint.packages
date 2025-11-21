---
id: localstack-s3
title: LocalStack S3 (S3-Compatible)
sidebar_label: LocalStack S3
---

LocalStack is a local AWS cloud emulator. Its S3 emulation is useful for development and automated tests.

This page shows how to configure AvantiPoint.Packages to use LocalStack S3 via the existing `AwsS3` storage provider.

## When to use LocalStack S3

Use LocalStack S3 when you:

- Want to test AWS-style infrastructure locally
- Already use LocalStack for other AWS services
- Need ephemeral S3 storage for integration tests

## Configuration

LocalStack exposes an S3-compatible endpoint on `http://localhost:4566` by default.

### Example `appsettings.json`

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-east-1",
    "Bucket": "packages",
    "AccessKey": "test",
    "SecretKey": "test",
    "ServiceUrl": "http://localhost:4566",
    "ForcePathStyle": true
  }
}
```

### Notes

- **ServiceUrl**: LocalStack edge endpoint for S3, usually `http://localhost:4566`.
- **ForcePathStyle**: Should be `true` so requests use path-style addressing.
- **Credentials**: LocalStack accepts any access/secret key values by default; `test` / `test` is common.

## Running LocalStack with S3

Refer to LocalStack docs for full details. A simple Docker command:

```bash
docker run --rm -it -p 4566:4566 localstack/localstack
```

Then create the `packages` bucket with the AWS CLI:

```bash
aws --endpoint-url=http://localhost:4566 s3 mb s3://packages
```

## Official documentation

- LocalStack S3 Coverage: https://docs.localstack.cloud/references/coverage/coverage_s3/
- LocalStack Docs: https://docs.localstack.cloud/

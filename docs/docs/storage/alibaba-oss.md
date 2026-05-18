---
id: alibaba-oss
title: Alibaba Cloud OSS (S3-Compatible)
sidebar_label: Alibaba OSS
---

Alibaba Cloud Object Storage Service (OSS) is a scalable object storage service. It offers an S3-compatible API via special endpoints.

This page shows how to configure AvantiPoint.Packages to use OSS’s S3-compatible API via the existing `AwsS3` storage provider.

## When to use Alibaba OSS (S3-Compatible)

Use OSS S3-compat mode when you:

- Host workloads on Alibaba Cloud
- Want to use the same S3-based storage abstraction everywhere

## Configuration

Alibaba exposes S3-compatible endpoints like:

- `https://oss-<region>.aliyuncs.com` (check docs for S3-compatible variations)

Refer to Alibaba’s documentation for the exact S3-compatible endpoint for your region.

### Example `appsettings.json`

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "cn-hangzhou",
    "Bucket": "my-oss-bucket",
    "AccessKey": "YOUR_ALIBABA_ACCESS_KEY_ID",
    "SecretKey": "YOUR_ALIBABA_ACCESS_KEY_SECRET",
    "ServiceUrl": "https://oss-cn-hangzhou.aliyuncs.com",
    "ForcePathStyle": true
  }
}
```

### Notes

- **ServiceUrl**: Endpoint for the OSS S3-compatible API in your region.
- **ForcePathStyle**: Some OSS S3-compatible endpoints work better with path-style; `true` is safer unless Alibaba docs state otherwise.
- Ensure that your bucket exists and is in the same region as the endpoint.

## Official documentation

- Alibaba Cloud OSS Docs: https://www.alibabacloud.com/help/en/object-storage-service/latest/use-oss-through-amazon-s3-applications



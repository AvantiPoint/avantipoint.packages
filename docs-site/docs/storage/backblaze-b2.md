---
id: backblaze-b2
title: Backblaze B2 (S3-Compatible)
sidebar_label: Backblaze B2
---

Backblaze B2 provides low-cost cloud object storage with an S3-compatible API.

This page shows how to configure AvantiPoint.Packages to use B2 via the existing `AwsS3` storage provider.

## When to use Backblaze B2

Use B2 when you:

- Want very inexpensive object storage
- Don’t mind slightly higher access latency vs major clouds
- Prefer Backblaze’s pricing model

## Configuration

Backblaze B2 exposes region-specific S3 endpoints, for example:

- `https://s3.us-west-002.backblazeb2.com`

Check Backblaze docs for the correct endpoint for your account/region.

### Example `appsettings.json`

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-west-002",
    "Bucket": "my-b2-bucket",
    "AccessKey": "YOUR_B2_KEY_ID",
    "SecretKey": "YOUR_B2_APPLICATION_KEY",
    "ServiceUrl": "https://s3.us-west-002.backblazeb2.com",
    "ForcePathStyle": false
  }
}
```

### Notes

- **Region**: Use your B2 region (e.g. `us-west-002`).
- **ServiceUrl**: Region endpoint for the S3-compatible API.
- **ForcePathStyle**: B2 supports virtual-hosted style; `false` is typically correct.
- You must create the bucket in B2 before using it.

## Official documentation

- Backblaze B2 S3 Compatible API: https://www.backblaze.com/docs/cloud-storage-s3-compatible-api



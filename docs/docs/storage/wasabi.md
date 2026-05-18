---
id: wasabi
title: Wasabi (S3-Compatible)
sidebar_label: Wasabi
---

Wasabi Hot Cloud Storage is an S3-compatible object storage service focused on low cost and high performance.

This page shows how to configure AvantiPoint.Packages to use Wasabi via the existing `AwsS3` storage provider.

## When to use Wasabi

Use Wasabi when you:

- Want predictable, low-cost object storage
- Prefer Wasabi’s pricing model (no egress/API fees) over AWS

## Configuration

Wasabi provides S3-compatible endpoints per region, for example:

- `https://s3.wasabisys.com` (US East 1)

Check Wasabi docs for the endpoint for your region.

### Example `appsettings.json`

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-east-1",
    "Bucket": "my-wasabi-bucket",
    "AccessKey": "YOUR_WASABI_ACCESS_KEY",
    "SecretKey": "YOUR_WASABI_SECRET_KEY",
    "ServiceUrl": "https://s3.wasabisys.com",
    "ForcePathStyle": false
  }
}
```

### Notes

- **ServiceUrl**: Region endpoint, e.g. `https://s3.wasabisys.com` or a region-specific variant.
- **ForcePathStyle**: Wasabi supports virtual-hosted style; `false` is typically correct.
- Ensure your bucket name and region match what you created in Wasabi.

## Official documentation

- Wasabi Docs: https://wasabi.com/docs/



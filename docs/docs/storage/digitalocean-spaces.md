---
id: digitalocean-spaces
title: DigitalOcean Spaces (S3-Compatible)
sidebar_label: DigitalOcean Spaces
---

DigitalOcean Spaces is an S3-compatible object storage service with built-in CDN support.

This page shows how to configure AvantiPoint.Packages to use Spaces via the existing `AwsS3` storage provider.

## When to use DigitalOcean Spaces

Use Spaces when you:

- Already host workloads on DigitalOcean
- Want simple, S3-compatible storage plus optional CDN
- Prefer DigitalOcean’s pricing/UX over AWS

## Configuration

Spaces exposes an S3-compatible endpoint per region, e.g. `https://nyc3.digitaloceanspaces.com`.

### Example `appsettings.json`

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "us-east-1",
    "Bucket": "my-space-name",
    "AccessKey": "YOUR_DO_SPACES_KEY",
    "SecretKey": "YOUR_DO_SPACES_SECRET",
    "ServiceUrl": "https://nyc3.digitaloceanspaces.com",
    "ForcePathStyle": false
  }
}
```

### Notes

- **Bucket**: Must match your Space name.
- **ServiceUrl**: Region endpoint, e.g. `https://nyc3.digitaloceanspaces.com`, `https://ams3.digitaloceanspaces.com`, etc.
- **ForcePathStyle**: Spaces supports virtual-hosted style URLs, so `false` is usually correct.

## Official documentation

- DigitalOcean Spaces Docs: https://docs.digitalocean.com/products/spaces/



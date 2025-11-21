---
id: s3-compatible
title: S3-Compatible Storage Providers
sidebar_label: S3-Compatible Providers
---

In addition to AWS S3, AvantiPoint.Packages can talk to many **S3-compatible storage providers** using the existing `AwsS3` storage provider plus two options:

- `ServiceUrl` – custom endpoint for the provider
- `ForcePathStyle` – whether to use path-style URLs

This page is an overview and links to provider-specific guides.

## How it works

All S3-compatible providers are configured like this:

```json
{
  "Storage": {
    "Type": "AwsS3",
    "Region": "<region>",
    "Bucket": "<bucket-or-space-name>",
    "AccessKey": "<access-key>",
    "SecretKey": "<secret-key>",
    "ServiceUrl": "<provider-endpoint>",
    "ForcePathStyle": true
  }
}
```

- **Type** must remain `AwsS3` – the same storage provider is used.
- **ServiceUrl** points to the provider’s S3-compatible endpoint.
- **ForcePathStyle** is `true` for most emulators (MinIO, LocalStack) and often `false` for hosted clouds (Spaces, Wasabi, B2).

## Provider guides

Use these dedicated pages for concrete examples and provider-specific notes:

- [MinIO](minio.md) – local / self-hosted S3-compatible storage
- [LocalStack S3](localstack-s3.md) – local AWS emulator for tests
- [DigitalOcean Spaces](digitalocean-spaces.md) – S3-compatible storage + CDN
- [Backblaze B2](backblaze-b2.md) – low-cost cloud object storage
- [Wasabi](wasabi.md) – hot cloud storage with S3 API
- [Alibaba Cloud OSS](alibaba-oss.md) – Alibaba’s object storage with S3 compatibility

## When to use S3-compatible providers

Consider S3-compatible providers when you:

- Want local emulation (MinIO, LocalStack)
- Prefer a different cloud vendor or pricing model
- Need to run storage on-premises or in a specific region not supported by AWS

If you are using **AWS S3 itself**, see the dedicated [AWS S3 Storage](awss3.md) page instead – it focuses purely on native S3. 



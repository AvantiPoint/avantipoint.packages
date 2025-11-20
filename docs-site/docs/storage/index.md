---
id: storage
title: Storage Configuration
sidebar_label: Overview
sidebar_position: 1
---

AvantiPoint Packages stores the actual package files (`.nupkg`) and symbol files (`.snupkg`) using a configured storage provider. The database stores metadata only.

## Supported Storage Providers

- **[File Storage](filestorage.md)** - Local file system or network share
- **[Azure Blob Storage](azureblob.md)** - Microsoft Azure cloud storage
- **[AWS S3](awss3.md)** - Amazon Web Services cloud storage

## Choosing a Storage Provider

| Feature | File Storage | Azure Blob | AWS S3 |
|---------|--------------|------------|--------|
| **Setup Complexity** | Simple | Medium | Medium |
| **Scalability** | Limited | Unlimited | Unlimited |
| **Multi-Instance** | Network share required | Yes | Yes |
| **Cost** | Server storage | Pay-as-you-go | Pay-as-you-go |
| **Best For** | Development, small deployments | Azure-hosted apps | AWS-hosted apps |

## Directory Structure

All storage providers organize packages in a similar structure:

```
packages/
  newtonsoft.json/
    12.0.1/
      newtonsoft.json.12.0.1.nupkg
  mypackage/
    1.0.0/
      mypackage.1.0.0.nupkg
symbols/
  newtonsoft.json/
    12.0.1/
      newtonsoft.json.12.0.1.snupkg
```

This structure allows:
- Easy browsing and debugging
- Simple migration between providers
- Clear organization by package ID and version

## Performance Tips

### Cloud Storage (Azure Blob / AWS S3)

- **Same Region**: Use the same region as your application server to minimize latency
- **CDN**: Enable Azure CDN or CloudFront for global distribution and caching
- **Storage Tiers**: Use hot tier for frequently accessed packages, cool tier for archives
- **Redundancy**: Configure appropriate redundancy (LRS, GRS, etc.) based on durability requirements

### File Storage

- **Fast Disks**: Use SSD storage for better I/O performance
- **Dedicated Storage**: Use dedicated storage to avoid contention with application files
- **Network Shares**: Use high-speed network connections for multi-instance deployments
- **Backup**: Implement regular backup procedures (snapshots, rsync, etc.)

## Migrating Between Providers

To migrate packages from one storage provider to another:

1. **Set up the new storage provider** with appropriate credentials and configuration
2. **Copy all files** from the old storage to the new storage
3. **Maintain the same directory structure** (`packages/` and `symbols/`)
4. **Update your configuration** to point to the new provider
5. **Test thoroughly** before switching in production

### Migration Tools

**File to Cloud:**
- Azure: Use [Azure Storage Explorer](https://azure.microsoft.com/en-us/products/storage/storage-explorer/) or `azcopy`
- AWS: Use AWS CLI `aws s3 sync` command

**Cloud to File:**
- Azure: Use `azcopy` or Storage Explorer
- AWS: Use `aws s3 sync`

**Cloud to Cloud:**
- Use provider-specific migration tools or intermediate file copy

## Security Considerations

- **Access Control**: Ensure storage is not publicly accessible unless intentional
- **Encryption**: Enable encryption at rest (automatic in Azure and AWS)
- **Credentials**: Use managed identities (Azure) or instance profiles (AWS) instead of keys
- **Network Security**: Use private endpoints or VPC peering for internal-only feeds
- **Audit Logging**: Enable storage audit logs for compliance

## Troubleshooting

### "Unable to connect to storage"

Check:
- Storage credentials are correct
- Network connectivity to storage endpoint
- Firewall rules allow access
- Managed identity or instance profile permissions

### "Permission denied"

Ensure the credentials have:
- Read permissions for package downloads
- Write permissions for package uploads
- List permissions for browsing

### Slow Performance

Consider:
- Using storage in the same region as your app
- Enabling CDN for frequently accessed packages
- Upgrading storage tier or I/O limits
- Checking network bandwidth and latency

## See Also

- [File Storage Configuration](filestorage.md)
- [Azure Blob Storage Configuration](azureblob.md)
- [AWS S3 Storage Configuration](awss3.md)
- [Database Configuration](../database/index.md)
- [Configuration Guide](configuration.md)

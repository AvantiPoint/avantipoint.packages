---
id: azureblob
title: Azure Blob Storage
sidebar_label: Azure Blob Storage
sidebar_position: 3
---

Store packages in Microsoft Azure Blob Storage for scalability and reliability.

## Package

First, add the Azure package:

```bash
dotnet add package AvantiPoint.Packages.Azure
```

## Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

Or use account name and access key separately:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "AccountName": "mystorageaccount",
    "AccessKey": "your-access-key-here"
  }
}
```

**Program.cs**:

```csharp
using AvantiPoint.Packages.Azure;

builder.Services.AddNuGetPackageApi(options =>
{
    options.AddAzureBlobStorage();
});
```

## Container Setup

The container name should be the name of the blob container you created in Azure Storage. The provider will create `packages/` and `symbols/` subdirectories within the container.

### Create Container

Using Azure CLI:

```bash
az storage container create --name packages --account-name myaccount
```

Or using Azure Portal:
1. Navigate to your storage account
2. Select "Containers"
3. Click "+ Container"
4. Name: `packages`
5. Public access level: `Private`

The container will be created automatically on first use if the account has appropriate permissions.

## Authentication Options

### 1. Connection String (Simple)

Most common, includes account name and key:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

Get the connection string from Azure Portal:
1. Navigate to storage account
2. Select "Access keys"
3. Copy "Connection string"

### 2. Account Name and Key

Separate the account name and key:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "AccountName": "mystorageaccount",
    "AccessKey": "your-access-key"
  }
}
```

### 3. Managed Identity (Recommended for Azure)

When running in Azure App Service, Azure Container Apps, Azure VMs, or AKS, use Managed Identity:

1. **Enable Managed Identity** on your Azure resource
   - For App Service: Settings → Identity → System assigned → On

2. **Grant permissions** to the identity:
   ```bash
   # Get the App Service principal ID
   PRINCIPAL_ID=$(az webapp identity show --name myapp --resource-group mygroup --query principalId -o tsv)
   
   # Grant Storage Blob Data Contributor role
   az role assignment create \
     --role "Storage Blob Data Contributor" \
     --assignee $PRINCIPAL_ID \
     --scope /subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.Storage/storageAccounts/{storage-account}
   ```

3. **Remove credentials** from configuration:
   ```json
   {
     "Storage": {
       "Type": "AzureBlobStorage",
       "Container": "packages",
       "AccountName": "mystorageaccount"
     }
   }
   ```

**Benefits:**
- No credentials to manage or rotate
- Automatic credential renewal
- Better security and compliance
- Follows Azure best practices

### 4. SAS Token (Limited Access)

For time-limited or restricted access:

```json
{
  "Storage": {
    "Type": "AzureBlobStorage",
    "Container": "packages",
    "AccountName": "mystorageaccount",
    "SasToken": "?sv=2021-06-08&ss=b&srt=sco&sp=rwdlac&se=2024-12-31T23:59:59Z&st=2024-01-01T00:00:00Z&spr=https&sig=..."
  }
}
```

Generate SAS token in Azure Portal:
1. Navigate to storage account
2. Select "Shared access signature"
3. Configure permissions and expiry
4. Click "Generate SAS and connection string"

## Storage Account Configuration

### Performance Tier

- **Standard**: General purpose, supports all storage types
- **Premium**: High-performance, low-latency (use for high-traffic feeds)

### Redundancy Options

- **LRS** (Locally Redundant): 3 copies in one datacenter (lowest cost)
- **ZRS** (Zone Redundant): 3 copies across availability zones
- **GRS** (Geo Redundant): 6 copies across regions (best durability)
- **GZRS** (Geo-Zone Redundant): ZRS + geo-replication

Recommended: **ZRS** for production (balance of cost and durability)

### Access Tier

- **Hot**: Frequently accessed data (use for active packages)
- **Cool**: Infrequently accessed (lower storage cost, higher access cost)
- **Archive**: Rarely accessed (lowest cost, hours to retrieve)

Use **Hot** tier for package feeds.

## CDN Integration

Enable Azure CDN for faster global downloads:

1. **Create CDN profile** in Azure Portal
2. **Create CDN endpoint** pointing to blob storage
3. **Update NuGet client** configuration to use CDN URL

Benefits:
- Faster downloads worldwide
- Reduced storage egress costs
- DDoS protection

## Performance Tips

### Same Region

Place storage in the same region as your application:

```bash
# Create storage account in same region as app
az storage account create \
  --name mystorageaccount \
  --resource-group mygroup \
  --location eastus \
  --sku Standard_ZRS
```

### Connection Pooling

Azure SDK automatically manages connections. Ensure you're reusing the `BlobServiceClient`:

```csharp
// Good: Service is registered as singleton
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddAzureBlobStorage();
});
```

### Firewall and Virtual Networks

For security, restrict access to specific networks:

1. Navigate to storage account
2. Select "Networking"
3. Change to "Selected networks"
4. Add your application's VNet or IP addresses

## Monitoring

Monitor these metrics in Azure Portal:

- **Transactions**: Request count and error rate
- **Availability**: Service uptime
- **Latency**: E2E and server latency
- **Capacity**: Storage used

Set up alerts for:
- High error rate
- Increased latency
- Storage capacity approaching limits

## Cost Optimization

### Lifecycle Management

Automatically move old packages to cool tier:

```json
{
  "rules": [
    {
      "name": "moveOldPackagesToCool",
      "enabled": true,
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["packages/"]
        },
        "actions": {
          "baseBlob": {
            "tierToCool": {
              "daysAfterModificationGreaterThan": 90
            }
          }
        }
      }
    }
  ]
}
```

### Reserved Capacity

Purchase reserved capacity for predictable workloads to save up to 38%.

## Troubleshooting

### "403 Forbidden" Errors

Check:
- Managed Identity has "Storage Blob Data Contributor" role
- Storage account firewall allows access
- SAS token is valid and not expired

### "404 Not Found" Errors

Check:
- Container name is correct
- Container exists in storage account
- Connection string points to correct account

### Slow Performance

Consider:
- Use Premium storage for high I/O
- Enable CDN for frequently accessed packages
- Check network latency between app and storage
- Verify storage is in same region

### Authentication Issues

Verify:
- Connection string is complete and correct
- Managed Identity is enabled and has permissions
- Firewall rules allow access from application

## Migration from File Storage

To migrate from file storage:

1. **Install Azure Storage Explorer** or use Azure CLI
2. **Upload files** maintaining directory structure:
   ```bash
   az storage blob upload-batch \
     --destination packages \
     --source /local/path/to/packages \
     --account-name mystorageaccount
   ```
3. **Update configuration** to use Azure Blob Storage
4. **Test thoroughly** before switching production

## See Also

- [Storage Overview](index.md)
- [File Storage Configuration](filestorage.md)
- [AWS S3 Storage Configuration](awss3.md)
- [Azure Storage Documentation](https://learn.microsoft.com/azure/storage/)

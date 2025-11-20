---
id: filestorage
title: File Storage
sidebar_label: File Storage
sidebar_position: 2
---

The default storage provider saves packages to the local file system or a network share.

## Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "FileStorage",
    "Path": "App_Data"
  }
}
```

**Program.cs**:

```csharp
builder.Services.AddNuGetPackageApi(options =>
{
    options.AddFileStorage();
});
```

## Path Options

The path can be:

### 1. Relative Path

Relative to the application directory:

```json
{
  "Storage": {
    "Path": "App_Data"
  }
}
```

This creates `App_Data` in the application's working directory.

### 2. Absolute Path

Full path to storage location:

**Linux/macOS:**
```json
{
  "Storage": {
    "Path": "/var/packages"
  }
}
```

**Windows:**
```json
{
  "Storage": {
    "Path": "D:\\PackageStorage"
  }
}
```

### 3. Network Share (Windows)

UNC path for shared storage:

```json
{
  "Storage": {
    "Path": "\\\\server\\share\\packages"
  }
}
```

**Note**: The application service account must have read/write permissions to the network share.

## Directory Structure

File storage creates this structure:

```
App_Data/
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

Each package and version has its own directory for isolation.

## Multi-Instance Deployments

For running multiple instances (load balancing):

1. **Use a network share** or NAS accessible to all instances
2. **Configure permissions** for the application service account
3. **Test write performance** to ensure no bottlenecks

Example configuration:

```json
{
  "Storage": {
    "Type": "FileStorage",
    "Path": "\\\\nas-server\\nuget-packages"
  }
}
```

Or use a cloud-based file share:
- Azure Files (SMB mount)
- AWS EFS (NFS mount)

## Performance Considerations

### Use SSD Storage

For best performance:
- Use SSD or NVMe drives for package storage
- Avoid spinning disks for production
- Monitor disk I/O and latency

### Dedicated Storage

- Use separate storage from OS and application files
- Avoid shared storage with high I/O workloads
- Consider RAID for redundancy and performance

### File System

**Windows:**
- Use NTFS with appropriate permissions
- Enable compression only if needed (trades CPU for disk space)

**Linux:**
- ext4 or XFS recommended
- Consider using `noatime` mount option for better performance

## Permissions

### Windows

Grant the application service account:
- **Modify** permissions on the storage directory
- Include subdirectories and files

```powershell
# Example: Grant IIS application pool identity
icacls "C:\PackageStorage" /grant "IIS APPPOOL\MyAppPool:(OI)(CI)M"
```

### Linux

Set ownership and permissions:

```bash
sudo mkdir -p /var/packages
sudo chown -R www-data:www-data /var/packages
sudo chmod -R 755 /var/packages
```

Or for a specific application user:

```bash
sudo chown -R myapp:myapp /var/packages
```

## Backup and Disaster Recovery

### Backup Strategy

1. **Regular backups** of the entire storage directory
2. **Incremental backups** for efficiency
3. **Off-site backups** for disaster recovery

### Backup Methods

**Windows:**
- Windows Backup
- Robocopy scripts
- Third-party tools (Veeam, Acronis, etc.)

**Linux:**
- rsync to backup location
- tar/gzip archives
- Backup tools (Bacula, Amanda, etc.)

Example rsync backup:

```bash
rsync -av --delete /var/packages/ /backup/packages/
```

### Restore

To restore:
1. Stop the application
2. Restore files to storage directory
3. Verify permissions
4. Start the application

## Monitoring

Monitor these metrics:

- **Disk space usage**: Alert when nearing capacity
- **Disk I/O**: Monitor read/write latency
- **File count**: Large numbers of small files can impact performance

### Disk Space Alerts

Set up alerts when disk usage exceeds thresholds:
- Warning: 75% full
- Critical: 90% full

## Maintenance

### Cleanup

Periodically clean up:
- Unlisted packages (if allowed)
- Old prerelease versions
- Orphaned files

### Defragmentation (Windows)

For spinning disks, periodically defragment:

```powershell
Optimize-Volume -DriveLetter D -Defrag -Verbose
```

Not needed for SSDs (can reduce lifespan).

## Troubleshooting

### "Access Denied" Errors

Check:
- Application service account has permissions
- Antivirus is not blocking file access
- Parent directory permissions are correct

### Slow Performance

Consider:
- Upgrade to SSD storage
- Check for disk I/O bottlenecks
- Reduce file system overhead (fewer directories/files)
- Use cloud storage for better scalability

### Network Share Issues

- Verify network connectivity
- Check share permissions
- Test write speed from application server
- Consider increasing network bandwidth

## When to Use File Storage

**Good for:**
- Development and testing
- Small deployments (< 100 packages)
- Single-instance applications
- Internal networks with fast storage

**Not recommended for:**
- Large-scale deployments
- Multi-region scenarios
- High-traffic feeds
- Environments requiring cloud-native features

## See Also

- [Storage Overview](index.md)
- [Azure Blob Storage Configuration](azureblob.md)
- [AWS S3 Storage Configuration](awss3.md)

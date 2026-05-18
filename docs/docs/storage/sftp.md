---
id: sftp
title: SFTP Storage
sidebar_label: SFTP
---

Store packages on a remote server via SFTP (SSH File Transfer Protocol).

## Package

```bash
dotnet add package AvantiPoint.Packages.Sftp
```

## Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "Sftp",
    "Host": "sftp.example.com",
    "Port": 22,
    "Username": "packages",
    "Password": "your-password",
    "RemotePath": "/var/packages",
    "MaxConnections": 4
  }
}
```

Passwordless auth:

```json
{
  "Storage": {
    "PrivateKeyPath": "/path/to/id_rsa",
    "PrivateKeyPassphrase": "optional"
  }
}
```

**Program.cs**:

```csharp
options.AddSftpStorage();
// or
options.AutoDiscoverSftpStorage();
```

## Behavior notes

- **Downloads:** `GetDownloadUriAsync` returns `null`. Clients download package content through the feed API (no CDN redirect).
- **Concurrency:** Use `MaxConnections` to limit parallel SSH sessions. SFTP is not ideal for high-concurrency feeds.
- **Paths:** Package paths are POSIX-style under `RemotePath` (for example `packages/{id}/{version}/...`).

## Local testing

```bash
docker run -p 2222:22 -d atmoz/sftp testuser:testpass:1001:1001:packages
```

Example test configuration:

```json
{
  "Storage": {
    "Type": "Sftp",
    "Host": "localhost",
    "Port": 2222,
    "Username": "testuser",
    "Password": "testpass",
    "RemotePath": "/"
  }
}
```

CI uses Testcontainers (`atmoz/sftp`) in `AvantiPoint.Packages.Storage.Tests`.

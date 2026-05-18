---
id: ftp
title: FTP / FTPS Storage
sidebar_label: FTP
---

Store packages on a remote FTP or FTPS server.

## Package

```bash
dotnet add package AvantiPoint.Packages.Ftp
```

## Configuration

**appsettings.json**:

```json
{
  "Storage": {
    "Type": "Ftp",
    "Host": "ftp.example.com",
    "Port": 21,
    "Username": "packages",
    "Password": "your-password",
    "RemotePath": "/packages",
    "UseSsl": false,
    "UsePassiveMode": true
  }
}
```

For FTPS, set `UseSsl` to `true` (explicit TLS).

Behind Docker or NAT, you may need `PassiveAddress` so the client connects to the correct data-channel host advertised by the server.

**Program.cs**:

```csharp
options.AddFtpStorage();
// or
options.AutoDiscoverFtpStorage();
```

## Behavior notes

- **Downloads:** `GetDownloadUriAsync` returns `null` (stream via the feed API).
- **Use case:** Legacy or low-traffic deployments only. Prefer object storage (S3, Azure, GCS) for production.

## Local testing

```bash
docker run -p 21:21 -p 21100-21105:21100-21105 \
  -e FTP_USER=testuser \
  -e FTP_PASS=testpass \
  -e PASV_ADDRESS=127.0.0.1 \
  -e PASV_MIN_PORT=21100 \
  -e PASV_MAX_PORT=21105 \
  fauria/vsftpd
```

CI uses Testcontainers with `fauria/vsftpd` and maps passive ports `21100`–`21105`.

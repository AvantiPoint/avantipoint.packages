---
id: hosting
title: Hosting
sidebar_label: Hosting
sidebar_position: 12
---

AvantiPoint Packages can be hosted in various environments. This guide covers common hosting scenarios.

## Production host (Docker)

The recommended production entry point is **`AvantiPoint.Packages.Host`** under `src/host/`. It includes database-backed API tokens, multi-provider UI authentication, email notifications, package source administration, and downstream syndication.

### Build and publish

The root [`Dockerfile`](https://github.com/AvantiPoint/avantipoint.packages/blob/main/Dockerfile) builds the registry image from the repository root:

```bash
docker build -t avantipoint/packages-host:latest .
docker tag avantipoint/packages-host:latest avantipoint/packages-host:<version>
docker push avantipoint/packages-host:latest
docker push avantipoint/packages-host:<version>
```

**Image defaults** (override at `docker run` or in your orchestrator):

| Variable | Default in image |
|----------|-------------------|
| `ASPNETCORE_URLS` | `http://+:8080` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

Keep `ASPNETCORE_ENVIRONMENT=Production` in production so configuration layers from `appsettings.json` and environment variables apply as designed. Set database, storage, authentication, email, and secrets via environment variables or your orchestrator's secret store—do not rely on dev placeholders in `appsettings.json`.

### Run the published image

Map port **8080** and persist package data under **`/data`** when using file-backed SQLite and `FileSystem` storage:

```bash
docker run -d -p 8080:8080 \
  -e Database__Type=Sqlite \
  -e Database__ConnectionString="Data Source=/data/packages.db" \
  -e Storage__Type=FileSystem \
  -e Storage__Path=/data/packages \
  -v feed-data:/data \
  avantipoint/packages-host:latest
```

**SQL Server** (managed instance or your own server; connection string points at the database host, not the container name unless you colocate):

```bash
docker run -d -p 8080:8080 \
  -e Database__Type=SqlServer \
  -e Database__ConnectionString="Server=<host>,1433;Database=packages;User Id=<user>;Password=<password>;TrustServerCertificate=True" \
  -e Storage__Type=FileSystem \
  -e Storage__Path=/data/packages \
  -v feed-data:/data \
  avantipoint/packages-host:latest
```

**PostgreSQL**:

```bash
docker run -d -p 8080:8080 \
  -e Database__Type=PostgreSQL \
  -e Database__ConnectionString="Host=<host>;Database=packages;Username=<user>;Password=<password>" \
  -e Storage__Type=FileSystem \
  -e Storage__Path=/data/packages \
  -v feed-data:/data \
  avantipoint/packages-host:latest
```

For cloud object storage (S3, Azure Blob, GCS, MinIO-compatible endpoints, and others), set `Storage__Type` and the provider-specific keys documented under [Storage](storage/index.md)—for example `Storage__Bucket`, `Storage__Region`, `Storage__ConnectionString`, or managed-identity flags—instead of mounting `/data/packages` for blobs.

Production secrets (examples): `Host__TokenHashPepper`, `Host__Authentication__Microsoft__ClientSecret`, `Host__Authentication__Google__ClientSecret`, `Host__Authentication__GitHub__ClientSecret`, and email provider API keys. Inject them via `-e`, Docker secrets, Kubernetes secrets, or your platform's secret store—never commit them to images or checked-in config.

### Configuration

Use double-underscore environment variables (see [configuration](configuration.md)):

- `Database__Type` — `Sqlite`, `SqlServer`, `PostgreSQL`, `MySql`
- `Database__ConnectionString`
- `Storage__Type`, `Storage__Path` (or cloud storage settings)
- `Host__Authentication__Microsoft`, `Host__Authentication__Google`, or `Host__Authentication__GitHub` — set `ClientId` and `ClientSecret` on one provider; the host auto-detects in order Microsoft → Google → GitHub. Omit all credentials to run without UI sign-in (local development)
- `EmailSettings__Provider` — see [Host email](host/email.md)

When UI authentication is configured, organizational membership is always enforced for the active provider:

| Provider | Organizational gate | Optional finer-grained access |
|----------|----------------------|------------------------------|
| Microsoft Account | Directory `TenantId` (not `common` / `consumers` / `organizations`); token `tid` must match | `AllowedEmailDomains`, `RequiredGroupIds` (Entra group object IDs in token `groups` claims when consented) |
| Google | `HostedDomain` (Google Workspace; OAuth `hd` claim) | `RequiredGroupIds` — **placeholder**; standard Google OAuth does not emit group membership in the ID token |
| GitHub | `Organization` (verified via GitHub API) | `TeamSlugs` |

**Google Workspace groups:** Unlike Microsoft Entra security groups, Google sign-in does not include group membership in the ID token. Restricting access to specific Google Groups requires the [Admin SDK Directory API](https://developers.google.com/admin-sdk/directory) or [Cloud Identity Groups API](https://cloud.google.com/identity/docs/groups) with a service account and domain-wide delegation (or equivalent admin consent)—not the end-user OAuth token alone. You may set `Host:Authentication:Google:RequiredGroupIds` to document intended groups for a future release; leave the list empty until Admin SDK integration is available.

Override structure or provider choice via double-underscore environment variables at runtime. The published image does not ship dev OAuth placeholders or sample secrets; configure every production value explicitly.

### Data Protection key persistence

Upstream package source credentials and downstream publish tokens are encrypted at rest using ASP.NET Core Data Protection. The key ring used for this encryption **must be persisted to a durable location** shared across restarts and (if load-balanced) instances — otherwise a lost key ring makes every previously-encrypted credential unreadable.

- `Host__DataProtection__KeyPath` — directory to persist keys to. The published Docker image sets this to `/data/dataprotection-keys` (alongside `/data/packages.db` on the same mounted volume), so container recreation is safe out of the box.
- `Host__DataProtection__ApplicationName` — optional; defaults to `AvantiPoint.Packages.Host`. Keep this stable across deployments/instances of the same feed — Data Protection scopes keys to the application name.
- If load balancing across multiple instances, point `KeyPath` at a shared/network file share, or replace the default file-system persistence with an [Azure Blob Storage](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers#azure-storage) or [Redis](https://learn.microsoft.com/aspnet/core/security/data-protection/implementation/key-storage-providers#redis) key-ring provider.
- The host logs a startup warning when `KeyPath` is not configured.

### Downstream publishing and syndication

Administrators can configure downstream registries under **Publish Targets**, then associate those targets with package groups under **Package Groups**. **Promote now** publishes the current group members on demand. A configured syndication association also publishes a new NuGet, npm, or OCI artifact automatically after its source upload succeeds.

Configure each target for the protocol accepted by the destination:

| Protocol | Publish endpoint | Credentials |
|----------|------------------|-------------|
| NuGet | A NuGet push endpoint or v3 service index, such as `https://api.nuget.org/v3/index.json` | API token |
| npm | A registry URL, such as `https://registry.npmjs.org` | Registry token |
| OCI | A registry or registry namespace URL without `/v2`, such as `https://ghcr.io/owner` | Username and token for Basic authentication, or a token without a username for direct Bearer authentication |

For OCI targets, the source repository name is appended to the configured endpoint. For example, promoting `tools/worker:1.2.0` to `https://ghcr.io/acme` publishes `ghcr.io/acme/tools/worker:1.2.0`. The publisher walks image indexes and manifests, uploads referenced content before its parent manifest, and skips blobs or manifests already present at the destination by digest. Destinations that challenge Basic authentication with a Bearer token flow are supported.

Publish tokens are encrypted using the Data Protection key ring described above. Keep the key ring durable and restrict access to the Publish Targets administration page.

### Health

`GET /health` runs checks against both the package catalog (`IContext`) and host identity (`IHostIdentityContext`) databases on the same connection.

### Project layout

```
src/host/
├── AvantiPoint.Packages.Host/           # Web app + Docker entrypoint
├── AvantiPoint.Packages.Host.Admin/    # Identity, email, auth, syndication
└── AvantiPoint.Packages.Host.Database.*  # Host identity EF migrations (4 providers)
```

`AvantiPoint.Packages.Server` remains a minimal reference host for development.

## Self-Hosted (On-Premises)

### Windows with IIS

1. **Install Prerequisites**:
   - .NET 10.0 Hosting Bundle
   - IIS with ASP.NET Core Module

2. **Publish your application**:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

3. **Create IIS Site**:
   - Create a new Application Pool with "No Managed Code"
   - Create a new website pointing to the publish folder
   - Set the Application Pool to the one created above

4. **Configure `web.config`**:
   The publish process creates this automatically, but you can customize it:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <location path="." inheritInChildApplications="false">
       <system.webServer>
         <handlers>
           <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
         </handlers>
         <aspNetCore processPath="dotnet" 
                     arguments=".\MyNuGetFeed.dll" 
                     stdoutLogEnabled="false" 
                     stdoutLogFile=".\logs\stdout" 
                     hostingModel="inprocess" />
       </system.webServer>
     </location>
   </configuration>
   ```

5. **Configure Storage**:
   For production, use a SQL Server database and configure file storage on a reliable drive:
   ```json
   {
     "Database": {
       "Type": "SqlServer"
     },
     "Storage": {
       "Type": "FileStorage",
       "Path": "D:\\PackageStorage"
     },
     "ConnectionStrings": {
       "SqlServer": "Server=localhost;Database=Packages;Integrated Security=true;"
     }
   }
   ```

6. **Configure upload size limits (package size)**:

   IIS and ASP.NET Core enforce request size limits that affect how large a `.nupkg` you can push. To increase the limit:

   - In `web.config`, adjust the IIS request filtering settings (values are in bytes):

   ```xml
   <configuration>
     <location path="." inheritInChildApplications="false">
       <system.webServer>
         <security>
           <requestFiltering>
             <!-- Example: allow up to 512 MB uploads -->
             <requestLimits maxAllowedContentLength="536870912" />
           </requestFiltering>
         </security>
         <aspNetCore processPath="dotnet"
                     arguments=".\MyNuGetFeed.dll"
                     stdoutLogEnabled="false"
                     stdoutLogFile=".\logs\stdout"
                     hostingModel="inprocess" />
       </system.webServer>
     </location>
   </configuration>
   ```

   - If you self-host with Kestrel (no IIS), you can also configure the max request body size in `Program.cs`:

   ```csharp
   builder.WebHost.ConfigureKestrel(options =>
   {
       // Example: allow up to 512 MB uploads
       options.Limits.MaxRequestBodySize = 512L * 1024L * 1024L;
   });
   ```

### Linux with Nginx

1. **Install .NET 10.0 Runtime**:
   ```bash
   wget https://dot.net/v1/dotnet-install.sh
   chmod +x dotnet-install.sh
   ./dotnet-install.sh --channel 10.0 --runtime aspnetcore
   ```

2. **Publish and Deploy**:
   ```bash
   dotnet publish -c Release -o /var/www/nuget-feed
   ```

3. **Create systemd Service**:
   Create `/etc/systemd/system/nuget-feed.service`:
   ```ini
   [Unit]
   Description=AvantiPoint NuGet Feed
   After=network.target

   [Service]
   WorkingDirectory=/var/www/nuget-feed
   ExecStart=/usr/bin/dotnet /var/www/nuget-feed/MyNuGetFeed.dll
   Restart=always
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=nuget-feed
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production

   [Install]
   WantedBy=multi-user.target
   ```

4. **Configure Nginx**:
   Create `/etc/nginx/sites-available/nuget-feed`:
   ```nginx
   server {
       listen 80;
       server_name packages.example.com;
       
       location / {
           proxy_pass http://localhost:5000;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection keep-alive;
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
           proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header X-Forwarded-Proto $scheme;
           
           # Allow large package uploads
           client_max_body_size 100M;
       }
   }
   ```

5. **Enable and Start**:
   ```bash
   sudo systemctl enable nuget-feed
   sudo systemctl start nuget-feed
   sudo ln -s /etc/nginx/sites-available/nuget-feed /etc/nginx/sites-enabled/
   sudo systemctl reload nginx
   ```

### Docker

For **`AvantiPoint.Packages.Host`**, use the root Dockerfile and [Production host (Docker)](#production-host-docker) above. For a custom feed generated from a template, build your own image from `dotnet publish` output and set `ASPNETCORE_ENVIRONMENT=Production` with the same double-underscore configuration variables.

## Cloud Hosting

### Azure App Service

1. **Create App Service**:
   - Runtime: .NET 10
   - Region: Choose closest to your users
   - Pricing Tier: B1 or higher recommended

2. **Configure Application Settings**:
   In the Azure Portal, add these settings:
   - `Database__Type`: `SqlServer`
   - `Storage__Type`: `AzureBlobStorage`
   - `Storage__Container`: `packages`

3. **Configure Connection Strings**:
   Add connection strings with type "SQLAzure":
   - Name: `SqlServer`
   - Value: Your Azure SQL connection string
   - Name: `Storage__ConnectionString` (or use Managed Identity)

4. **Deploy**:
   Using Azure CLI:
   ```bash
   az webapp deployment source config-zip \
     --resource-group myResourceGroup \
     --name myNuGetFeed \
     --src ./publish.zip
   ```

5. **Configure Managed Identity** (Recommended):
   - Enable System Assigned Managed Identity on your App Service
   - Grant the identity access to your Azure SQL and Blob Storage
   - Remove `Storage__ConnectionString` from config (uses Managed Identity automatically)

### AWS Elastic Beanstalk

1. **Install Prerequisites**:
   Add the AWS package:
   ```bash
   dotnet add package AvantiPoint.Packages.Aws
   ```

2. **Configure for AWS**:
   Update `appsettings.json`:
   ```json
   {
     "Database": {
       "Type": "SqlServer"
     },
     "Storage": {
       "Type": "AwsS3",
       "Region": "us-west-2",
       "Bucket": "my-nuget-packages",
       "UseInstanceProfile": true
     }
   }
   ```

3. **Create IAM Role**:
   Create a role for your Elastic Beanstalk instances with:
   - S3 access to your bucket
   - RDS access if using RDS

4. **Deploy**:
   ```bash
   dotnet eb deploy
   ```

### AWS EC2 with S3 and RDS

This is similar to the Linux self-hosted setup, but with AWS-specific configuration:

1. **Set up EC2 instance** with .NET 10 runtime

2. **Configure IAM Role** for the instance with S3 and RDS access

3. **Update configuration**:
   ```json
   {
     "Database": {
       "Type": "SqlServer"
     },
     "Storage": {
       "Type": "AwsS3",
       "Region": "us-west-2",
       "Bucket": "my-packages",
       "UseInstanceProfile": true
     },
     "ConnectionStrings": {
       "SqlServer": "Server=mydb.xxxx.us-west-2.rds.amazonaws.com;Database=packages;User Id=admin;Password=..."
     }
   }
   ```

4. **Use the instance profile for authentication** (no credentials in config)

## Performance Considerations

### Database

- Use connection pooling (enabled by default)
- For high traffic, consider read replicas
- Regular index maintenance for SQL Server/MySQL

### Storage

- **File Storage**: Use fast SSD storage, consider NAS/SAN for multiple instances
- **Cloud Storage**: Use the same region as your application
- Enable CDN for package downloads in high-traffic scenarios

### Caching

Configure output caching for metadata responses (add to `Program.cs`):

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Cache());
});

var app = builder.Build();

app.UseOutputCache();
app.UseRouting();
```

### Load Balancing

For multiple instances:
- Use a shared database (SQL Server, MySQL, or PostgreSQL)
- Use cloud storage (S3 or Azure Blob)
- Configure session affinity on your load balancer (not required, but can improve performance)

## SSL/TLS Configuration

**Always use HTTPS in production!** NuGet credentials are sent in headers.

### Let's Encrypt with Nginx

```bash
sudo apt install certbot python3-certbot-nginx
sudo certbot --nginx -d packages.example.com
```

### Azure App Service

SSL is automatic with custom domains. Just add your domain and Azure handles the certificate.

### AWS with Application Load Balancer

Use AWS Certificate Manager for free SSL certificates, attach to your ALB.

## Monitoring

Consider adding:
- Application Insights (Azure) or CloudWatch (AWS)
- Health check endpoints
- Logging to file/database/cloud

The production Host registers database health checks for both contexts and exposes `GET /health` (see [Production host (Docker)](#production-host-docker) above).

## See Also

- [Configuration](configuration.md) - Detailed configuration options
- [Database](database/index.md) - Database setup
- [Storage](storage/index.md) - Storage configuration

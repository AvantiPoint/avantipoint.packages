# Hosting

AvantiPoint Packages can be hosted in various environments. This guide covers common hosting scenarios.

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

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["MyNuGetFeed.csproj", "./"]
RUN dotnet restore "MyNuGetFeed.csproj"
COPY . .
RUN dotnet build "MyNuGetFeed.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyNuGetFeed.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyNuGetFeed.dll"]
```

Build and run:

```bash
docker build -t my-nuget-feed .
docker run -d -p 5000:80 -v /data/packages:/app/App_Data my-nuget-feed
```

With Docker Compose (`docker-compose.yml`):

```yaml
version: '3.8'
services:
  nuget-feed:
    build: .
    ports:
      - "5000:80"
    volumes:
      - packages-data:/app/App_Data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Type=Sqlite
      - ConnectionStrings__Sqlite=Data Source=/app/App_Data/packages.db
    restart: unless-stopped

volumes:
  packages-data:
```

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

Add health checks in `Program.cs`:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<Context>();

var app = builder.Build();

app.MapHealthChecks("/health");
```

## See Also

- [Configuration](configuration.md) - Detailed configuration options
- [Database](database.md) - Database setup
- [Storage](storage.md) - Storage configuration

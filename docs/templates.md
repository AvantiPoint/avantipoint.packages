# Templates

AvantiPoint provides project templates to help you get started quickly with a fully-featured, production-ready NuGet feed.

## Package Feed Template

The complete package feed template includes:

- Azure Active Directory integration for authentication
- API token management (create, revoke, regenerate)
- SendGrid email notifications for:
  - Package publishing
  - Token creation/revocation
  - New IP address detection
- Token usage tracking
- User interface for token management

### Installation

Install the template package:

```powershell
dotnet new install AvantiPoint.Packages.Templates
```

Check available templates:

```powershell
dotnet new list avantipoint
```

### Creating a New Feed

Create a new project from the template:

```powershell
dotnet new avantipoint-feed -n MyCompanyFeed
cd MyCompanyFeed
```

### Configuration

The template requires configuration for:

1. **Azure AD** - For user authentication
   ```json
   {
     "AzureAd": {
       "Instance": "https://login.microsoftonline.com/",
       "TenantId": "your-tenant-id",
       "ClientId": "your-client-id",
       "CallbackPath": "/signin-oidc"
     }
   }
   ```

2. **SendGrid** - For email notifications
   ```json
   {
     "SendGrid": {
       "ApiKey": "your-sendgrid-api-key",
       "FromEmail": "noreply@yourcompany.com",
       "FromName": "Your Company Package Feed"
     }
   }
   ```

3. **Database** - SQL Server connection
   ```json
   {
     "ConnectionStrings": {
       "SqlServer": "Server=localhost;Database=PackageFeed;Integrated Security=true;"
     }
   }
   ```

### Features

#### API Token Management

Users can manage their API tokens through a web interface:
- Create new tokens with custom names
- View all active tokens
- Revoke tokens
- See token usage history

#### Email Notifications

Automatic email notifications for:
- **Token Created**: Sent when a user creates a new API token
- **Token Revoked**: Sent when a token is revoked
- **Token Regenerated**: Sent when a token is regenerated
- **Package Published**: Sent when a user publishes a package
- **New IP Address**: Sent when a token is used from a new IP address

#### Security Features

- IP address tracking for all token usage
- Token expiration support
- Token revocation
- Azure AD single sign-on
- Automatic email alerts for suspicious activity

### Customization

The template provides a starting point. You can customize:

- **Authentication**: Switch from Azure AD to another provider
- **Email**: Switch from SendGrid to another email service
- **UI**: Customize the web interface
- **Business Logic**: Modify token policies, IP tracking, etc.

### Running the Template

1. **Configure settings** in `appsettings.json`
2. **Create the database**:
   ```bash
   dotnet ef database update
   ```
3. **Run the application**:
   ```bash
   dotnet run
   ```
4. **Access the web interface** at `https://localhost:5001`

### Deployment

The template is ready to deploy to:
- Azure App Service
- IIS
- Linux with Nginx
- Docker containers

See [Hosting](hosting.md) for deployment guides.

## Building Your Own Template

If the provided template doesn't meet your needs, you can create your own:

1. **Start with a sample** - Use `OpenFeed` or `AuthenticatedFeed` from the samples
2. **Add your features** - Authentication, UI, business logic
3. **Create a template** - Package it as a `dotnet new` template
4. **Share** - Publish to NuGet.org or your private feed

### Creating a Template Package

1. Create a `.template.config` folder in your project
2. Add `template.json`:
   ```json
   {
     "name": "My NuGet Feed",
     "identity": "MyCompany.Packages.Template",
     "shortName": "mycompany-feed",
     "tags": {
       "language": "C#",
       "type": "project"
     },
     "sourceName": "MyCompanyFeed",
     "preferNameDirectory": true
   }
   ```
3. Package and publish:
   ```bash
   dotnet pack
   dotnet nuget push MyCompany.Packages.Template.1.0.0.nupkg
   ```

## See Also

- [Getting Started](getting-started.md) - Build a feed from scratch
- [Authentication](authentication.md) - Implement authentication
- [Hosting](hosting.md) - Deploy your feed
- [Samples](https://github.com/AvantiPoint/avantipoint.packages/tree/main/samples) - Example projects

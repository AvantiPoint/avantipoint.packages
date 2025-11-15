# AvantiPoint Packages

AvantiPoint Packages is a modern, extensible NuGet package feed server built on .NET. It evolved from the BaGet project with significant enhancements for real-world line of business applications.

## Why AvantiPoint Packages?

While AvantiPoint Packages is based on the BaGet project, it has been extensively enhanced to meet the needs of professional development teams and commercial vendors. Whether you're managing an internal package feed, providing packages to customers, or running a subscription-based package service like SponsorConnect, AvantiPoint Packages provides the features you need.

## Key Features

### 1. Advanced Authentication

Secure your package feed with flexible authentication options. Control who can access your packages with role-based permissions for package consumers and publishers.

### 2. Fine-Grained Authorization

Beyond knowing who the user is, control what they can do:
- **Role-based Access**: Separate permissions for package consumers and publishers
- **Package-level Control**: Restrict access to specific packages based on user licenses or subscriptions
- **Extensible**: Implement `IPackageAuthenticationService` to integrate with your existing authentication system

### 3. Lifecycle Event Hooks

React to package and symbol upload/download events through the `INuGetFeedActionHandler` interface:
- Send email notifications when packages are published
- Track download metrics for analytics
- Monitor for security concerns (new IP addresses, unusual activity)
- Implement custom business logic

### 4. Multiple Upstream Sources

Configure multiple upstream NuGet feeds (authenticated or public):
- Mirror NuGet.org for offline/cached access
- Include commercial feeds (Telerik, Infragistics, Syncfusion, etc.)
- Consolidate multiple feeds into a single endpoint for your team

### 5. Cloud-Ready Hosting

Built-in support for modern cloud platforms:
- **AWS**: S3 storage with flexible authentication options
- **Azure**: Blob storage integration
- **On-Premises**: File system or network share storage

### 6. Modern .NET

- Built on .NET 10.0 for best performance and latest features
- Follows current .NET best practices and patterns
- Regular updates to keep dependencies current

## Use Cases

**For Enterprise Teams**: Secure your intellectual property with authenticated feeds, track usage, and integrate with your existing identity provider.

**For Component Vendors**: Provide licensed packages to customers, control access based on subscriptions, and monitor usage patterns.

**For SaaS Platforms**: Power subscription-based package distribution with user management and automated licensing.

## Next Steps

- [Getting Started](registration.md) - Set up your first feed
- [Authentication](authentication.md) - Secure your feed
- [Configuration](configuration.md) - Configure database, storage, and more
- [Hosting](hosting.md) - Deploy to AWS, Azure, or on-premises

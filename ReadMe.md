# AvantiPoint Packages

AvantiPoint Packages is a modern, production-focused NuGet + symbols server derived from the excellent [BaGet](https://github.com/loic-sharma/BaGet) project. It powers several AvantiPoint and partner commercial feeds and extends BaGet with advanced authentication, event callbacks, performance optimizations, and enterprise-oriented configuration.

> Goal: Make it *ridiculously simple* to stand up a secure, extensible NuGet feed while letting you plug in your own user/auth logic with minimal ceremony.

## Attribution & Origins

This repository began as a fork of BaGet (MIT). Enormous credit and thanks go to BaGet's creator and contributors for their foundational work on a lightweight, cloud‑native NuGet server. AvantiPoint Packages keeps BaGet's spirit (simple, fast, cloud‑friendly) while layering on features needed for commercial and multi‑tenant scenarios.

### Maintenance Status

BaGet's last tagged release (`v0.4.0-preview2`) shipped in **September 2021** and the repository has seen minimal code activity since, effectively leaving it unmaintained. In contrast, **AvantiPoint Packages is actively maintained** and already targets **.NET 10.0** (released November 2025), receiving ongoing security, performance, and feature updates aligned with the evolving NuGet ecosystem.

## Production Feeds Using This Codebase

These deployments demonstrate real-world usage and ongoing hardening:

- AvantiPoint's Internal NuGet Server
- AvantiPoint's Enterprise Support NuGet Server
- Sponsor Connect NuGet Server ([SponsorConnect](https://sponsorconnect.dev))
- Prism Library Commercial Plus NuGet Server

## Key Enhancements Over BaGet

Below are the major areas where AvantiPoint Packages diverges and improves upon BaGet:

- Modern Target Framework: Upgraded from `netcoreapp3.1` to current .NET (currently **.NET 10.0**), enabling latest language/runtime features.
- Central Package Management: Unified dependency versioning via `Directory.Packages.props` for consistent builds across solutions.
- Advanced Authentication Layer:
    - Authentication logic is **decoupled from ASP.NET Core Auth** so it can drop into existing sites with minimal friction.
    - Dual model: API Key (publish) + Basic (consume) with fine-grained per-user/per-token permissions.
    - Pluggable via a single interface: `IPackageAuthenticationService`.
- Event Callback Hooks:
    - Upload/Download interception through `INuGetFeedActionHandler` for metrics, auditing, compliance, notifications, business rules, package filtering, or symbols tracking.
- Performance Optimizations & Data Model Enhancements:
    - Database views for aggregated queries (latest versions, download counts, search info) reduce N+1 patterns and heavy joins.
    - Guidance on indexing, composite filters, and query batching (see `docs/performance-optimization.md`).
- Multi-Storage & Provider Flexibility:
    - Azure Blob, AWS S3, plus multiple relational database providers (SQL Server, SQLite, MySQL) with extensible patterns for adding more.
- Enterprise Integration Readiness:
    - Clear separation of concerns (Core, Hosting, Protocol, Storage providers) for easier customization and versioning.
    - Callback + Auth surfaces designed for audit trails and external telemetry enrichment.
- Active Maintenance for Commercial Feeds:
    - Hardened through real-world traffic, CI usage, and authenticated private package flows. Actively updated (currently on .NET 10.0) whereas upstream BaGet has seen little activity since 2021.

## Why We Forked

Commercial scenarios (paid subscriptions, enterprise support tiers, per-user publish rights, granular token revocation) required deeper user and event lifecycle control than was practical to upstream without complicating BaGet's lightweight mission. Forking allowed rapid iteration on authentication semantics and performance while keeping the public surface area intentionally narrow and purposeful.

## High-Level Architecture

Projects are organized by responsibility:

- `AvantiPoint.Packages.Core`: Core abstractions, auth, domain models.
- `AvantiPoint.Packages.Protocol`: NuGet protocol implementation details.
- `AvantiPoint.Packages.Hosting`: ASP.NET Core hosting integration & DI bootstrapping.
- Storage providers: `Azure`, `Aws`, `Database.*` (SQL Server, SQLite, MySQL).
- Samples (`samples/`): Turn‑key feed variants (open vs authenticated).

Documentation lives under `docs/` and is published via `mkdocs.yml` to the project site.

## Quick Start

Choose a sample depending on whether you need authentication:

- **OpenFeed** – Public, unauthenticated feed.
- **AuthenticatedFeed** – Private feed with auth + callbacks.

Restore & run (root solution):

```pwsh
dotnet restore APPackages.slnx
dotnet build APPackages.slnx
dotnet run --project samples/OpenFeed/OpenFeed.csproj
dotnet run --project samples/AuthenticatedFeed/AuthenticatedFeed.csproj
```

Then browse the feed base URL (default `http://localhost:5000/`). See full docs: https://avantipoint.github.io/avantipoint.packages/

## NuGet Packages

Latest versions (including prerelease) for all published packages in this repo:

| Package | NuGet.org | CI Feed |
| --- | --- | --- |
| AvantiPoint.Packages.Aws | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Aws.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Aws/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Aws/vpre) |
| AvantiPoint.Packages.Azure | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Azure.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Azure/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Azure/vpre) |
| AvantiPoint.Packages.Core | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Core.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Core/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Core/vpre) |
| AvantiPoint.Packages.Database.MySql | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.MySql.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.MySql/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.MySql/vpre) |
| AvantiPoint.Packages.Database.PostgreSql | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.PostgreSql.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.PostgreSql/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.PostgreSql/vpre) |
| AvantiPoint.Packages.Database.SqlServer | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.SqlServer.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.SqlServer/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.SqlServer/vpre) |
| AvantiPoint.Packages.Database.Sqlite | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.Sqlite.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.Sqlite/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.Sqlite/vpre) |
| AvantiPoint.Packages.Hosting | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Hosting.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Hosting/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Hosting/vpre) |
| AvantiPoint.Packages.Protocol | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Protocol.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Protocol/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Protocol/vpre) |
| AvantiPoint.Packages.Signing.Aws | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Signing.Aws.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Signing.Aws/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Signing.Aws/vpre) |
| AvantiPoint.Packages.Signing.Azure | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Signing.Azure.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Signing.Azure/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Signing.Azure/vpre) |
| AvantiPoint.Packages.Signing.Gcp | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Signing.Gcp.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Signing.Gcp/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Signing.Gcp/vpre) |
| AvantiPoint.Packages.UI.Razor | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.UI.Razor.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.UI.Razor/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.UI.Razor/vpre) |

---

## Authentication

To authenticate your users you will need to implement `IPackageAuthenticationService`. If you're new to creating your own NuGet feed then it's important that you understand a few basics here on authentication with NuGet feeds.

While there are indeed custom authentication brokers that you could provide the NuGet client this can provide a lot more complexity than should otherwise be needed. For this reason we try to stick to some standards for what the NuGet client expects out of box. With that said we recognize 2 user roles currently.

1) Package Consumer
2) Package Publisher

> **NOTE:** When we say Roles, we do not use AspNetCore Authentication mechanisms, and thus do not care in any shape, way, or form about the Role claims of the ClaimsPrincipal.

While what each role can do should be pretty self explanatory how they are authenticated probably isn't.

As a standard for user authentication we expect a Basic authentication scheme for PackageConsumers. For package publishing however the NuGet client has a limitation that it only supports passing the Api Key via a special header which it handles. For this reason the `IPackageAuthenticationService` provides two methods. When simply validating a user based on the ApiToken it should be assumed that you are publishing and therefore if the user that the token belongs to does not have publishing rights you should return a failed result.

## Callbacks

In addition to User Authentication, AvantiPoint Packages offers a Callback API to allow you to handle custom logic such as sending emails or additional context tracking. To hook into these events you just need to register a delegate for `INuGetFeedActionHandler`.

## Getting Started

For a quick start, see our sample projects:
- **OpenFeed** - A simple, open NuGet feed without authentication
- **AuthenticatedFeed** - A secured feed with authentication and callbacks

For detailed documentation, including setup guides, configuration options, and advanced features, visit the [documentation site](https://avantipoint.github.io/avantipoint.packages/).

## Samples

While we have a basic implementation of the IPackageAuthenticationService in the AuthenticatedFeed sample... below is a sample to give you a little more complexity and understanding of how you might use this to authenticate your users.

```c#
public class MyAuthService : IPackageAuthenticationService
{
    private MyDbContext _db { get; }

    public MyAuthService(MyDbContext db)
    {
        _db = db;
    }

    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
    {
        var token = await _db.PackageTokens
                            .Include(x => x.User)
                            .ThenInclude(x => x.Permissions)
                            .FirstOrDefaultAsync(x => x.Token == apiKey);

        if (token is null || token.IsExpiredOrRevoked())
        {
            return NuGetAuthenticationResult.Fail("Unknown user or Invalid Api Token.", "Contoso Corp Feed");
        }

        if (!token.User.Permissions.Any(x => x.Name == "PackagePublisher"))
        {
            return NuGetAuthenticationResult.Fail("User is not authorized to publish packages.", "Contoso Corp Feed");
        }

        var identity = new ClaimsIdentity("NuGetAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, token.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, token.User.Email));

        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }

    public async Task<NuGetAuthenticationResult> AuthenticateAsync(string username, string token, CancellationToken cancellationToken)
    {
        var apiToken = await _db.PackageTokens
                            .Include(x => x.User)
                            .ThenInclude(x => x.Permissions)
                            .FirstOrDefaultAsync(x => x.Token == token && x.User.Email == username);

        if (apiToken is null || apiToken.IsExpiredOrRevoked())
        {
            return NuGetAuthenticationResult.Fail("Unknown user or Invalid Api Token.", "Contoso Corp Feed");
        }

        if (!apiToken.User.Permissions.Any(x => x.Name == "PackageConsumer"))
        {
            return NuGetAuthenticationResult.Fail("User is not authorized.", "Contoso Corp Feed");
        }

        var identity = new ClaimsIdentity("NuGetAuth");
        identity.AddClaim(new Claim(ClaimTypes.Name, apiToken.User.Name));
        identity.AddClaim(new Claim(ClaimTypes.Email, apiToken.User.Email));

        return NuGetAuthenticationResult.Success(new ClaimsPrincipal(identity));
    }
}
```

It's worth noting here that AvantiPoint Packages itself does not care at all about the ClaimsPrincipal, however if you provide one to the NuGetAuthenticationResult it will set this to the HttpContext so that it is available to you in your callbacks.

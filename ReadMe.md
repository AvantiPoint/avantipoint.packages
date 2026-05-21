# AvantiPoint Packages

AvantiPoint Packages is a modern, production-focused **multi-protocol artifact feed platform** (NuGet, npm, OCI) derived from the excellent [BaGet](https://github.com/loic-sharma/BaGet) project ([Bagetter](https://github.com/bagetter/Bagetter) is the official community fork). It powers several AvantiPoint and partner commercial feeds and extends BaGet with advanced authentication, event callbacks, performance optimizations, and enterprise-oriented configuration.

> Goal: Make it *ridiculously simple* to stand up a secure, extensible feed on **one hostname**—NuGet at the root, npm under `/npm`, OCI at `/v2/` and optional named segments—while letting you plug in your own user/auth logic with minimal ceremony.

## Supported protocol surfaces

One deployment can expose multiple **surfaces** on the same origin (shared storage, database, and auth):

| Protocol | Default path | Clients |
|----------|--------------|---------|
| **NuGet** | `/v3/index.json` (legacy push: `api/v2/package`) | `dotnet`, NuGet.exe |
| **npm** | `/npm/` | `npm`, `pnpm`, `yarn` |
| **OCI** (default) | `/v2/` | `docker`, `helm`, `oras`, `crane` |
| **OCI** (named) | `/{segment}/v2/` (e.g. `/docker/v2/`, `/helm/v2/`) | Same; segment isolates catalogs |

Surfaces are registered in code via `AvantiPoint.Feed.Platform` (`AddAvantiPointFeed`, `UseNuGet`, `UseNpmRegistry`, `UseOciRegistry` / `UseConfiguredOciSurfaces`) and mapped with `MapNuGetApiRoutes`, `MapNpmFeed`, and `MapOciFeed`. See [npm registry](docs/docs/feeds/npm-registry.md), [multi-feed UI](docs/docs/feeds/multi-feed-ui.md), and `.cursor/MULTI-FEED-PLATFORM-SPEC.md` for design detail.

**Note:** NuGet legacy push uses `api/v2/package`—that is **not** the OCI Distribution API, which lives under `/v2/` at the site root or under a named segment.

## Attribution & Origins

This repository began as a fork of BaGet (MIT). Enormous credit and thanks go to BaGet's creator and contributors for their foundational work on a lightweight, cloud‑native NuGet server. Community maintenance of that lineage continues through [Bagetter](https://github.com/bagetter/Bagetter), the official community fork of BaGet. AvantiPoint Packages keeps BaGet's spirit (simple, fast, cloud‑friendly) while layering on features needed for commercial and multi‑tenant scenarios.

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
- Multi-protocol feed platform:
    - One host, many surfaces: NuGet (unchanged root URLs), npm (`/npm`), OCI Distribution (`/v2/` default and `/{segment}/v2/` named registries for Docker, Helm, and generic OCI artifacts).
    - Shared storage, database, and authentication across protocols; protocol-specific packages (`AvantiPoint.Packages.Registry.Npm`, `AvantiPoint.Packages.Registry.Oci`).
- Active Maintenance for Commercial Feeds:
    - Hardened through real-world traffic, CI usage, and authenticated private package flows. Actively updated (currently on .NET 10.0) whereas upstream BaGet has seen little activity since 2021.

## Why We Forked

Commercial scenarios (paid subscriptions, enterprise support tiers, per-user publish rights, granular token revocation) required deeper user and event lifecycle control than was practical to upstream without complicating BaGet's lightweight mission. Forking allowed rapid iteration on authentication semantics and performance while keeping the public surface area intentionally narrow and purposeful.

## High-Level Architecture

Projects are organized by responsibility:

- `AvantiPoint.Packages.Core`: Core abstractions, auth, domain models.
- `AvantiPoint.Packages.Protocol`: NuGet protocol implementation details.
- `AvantiPoint.Feed.Platform`: Multi-surface feed registration, shared auth, and middleware.
- `AvantiPoint.Packages.Registry.Npm` / `AvantiPoint.Packages.Registry.Oci`: npm and OCI Distribution protocol implementations.
- `AvantiPoint.Packages.Hosting`: ASP.NET Core hosting integration & DI bootstrapping.
- Storage providers: `Azure`, `Aws`, `Database.*` (SQL Server, SQLite, MySQL, PostgreSQL).
- `src/host/AvantiPoint.Packages.Host`: Production multi-feed host (config-driven surfaces).
- Samples (`samples/`): Turn‑key feed variants (open multi-feed, authenticated NuGet, API-only test host).

Documentation lives under `docs/` and is published to the [documentation site](https://avantipoint.github.io/avantipoint.packages/).

## Quick Start

| Host | Protocols | Use when |
|------|-----------|----------|
| **[OpenFeed](samples/OpenFeed)** | NuGet + npm + OCI (default `/v2/`) | Local dev; Blazor UI for all surfaces |
| **[Packages.Host](src/host/AvantiPoint.Packages.Host)** | Config-driven (`Feed:Npm`, `Feed:Oci:*`) | Production-style multi-feed deployment |
| **[IntegrationTestApi](samples/IntegrationTestApi)** | NuGet + npm + OCI + `/docker/v2/` (API only) | Headless host for integration tests |
| **[AuthenticatedFeed](samples/AuthenticatedFeed)** | NuGet only | Auth + callbacks sample |
| **Aspire AppHost** (`AvantiPoint.Packages.AppHost`) | NuGet only today | Local Aspire orchestration (DB/storage providers) |

Restore & run (root solution):

```pwsh
dotnet restore APPackages.slnx
dotnet build APPackages.slnx
dotnet run --project samples/OpenFeed/OpenFeed.csproj
```

Browse the site root (default `https://localhost:5001/`): NuGet search UI, npm browse at `/npm`, OCI catalog at `/oci`. NuGet service index: `/v3/index.json`.

Full docs: https://avantipoint.github.io/avantipoint.packages/

### Multi-feed configuration (high level)

Surfaces and shared auth are configured under `Feed` in `appsettings.json` (production host) or registered in `Program.cs` (samples):

```json
{
  "Feed": {
    "PublicBaseUrl": "https://packages.example.com",
    "Authentication": {
      "ApiKey": "your-token",
      "AllowAnonymousPull": false
    },
    "Npm": { "Enabled": true },
    "Oci": {
      "Default": { "Enabled": true },
      "Docker": { "Enabled": true },
      "Helm": { "Enabled": true }
    }
  }
}
```

On **Packages.Host**, `UseNpmRegistryIfEnabled` and `UseConfiguredOciSurfaces` read these flags. **OpenFeed** enables NuGet, npm, and OCI in code for a turnkey demo.

### Clients (one-liners)

Point each client at your feed origin (replace host/port):

```bash
dotnet nuget add source https://localhost:5001/v3/index.json --name LocalFeed
npm config set registry https://localhost:5001/npm/
docker login localhost:5001
helm registry login localhost:5001/helm
```

Protocol-specific setup, auth, and UI snippets: [npm registry](docs/docs/feeds/npm-registry.md), [multi-feed UI](docs/docs/feeds/multi-feed-ui.md).

### Testing

Registry integration tests exercise real toolchains when installed (`dotnet`/`nuget`, `npm`, `docker`, `helm`). See `tests/AvantiPoint.Packages.Registry.Npm.Tests`, `tests/AvantiPoint.Packages.Registry.Oci.Tests`, and `tests/AvantiPoint.Packages.Protocol.Tests`.

## NuGet Packages

Latest versions (including prerelease) for all published packages in this repo:

| Package | NuGet.org | CI Feed |
| --- | --- | --- |
| AvantiPoint.Packages.Aws | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Aws.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Aws/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Aws/vpre) |
| AvantiPoint.Packages.Azure | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Azure.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Azure/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Azure/vpre) |
| AvantiPoint.Feed.Platform | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Feed.Platform.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Feed.Platform/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Feed.Platform/vpre) |
| AvantiPoint.Packages.Core | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Core.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Core/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Core/vpre) |
| AvantiPoint.Packages.Database.MySql | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.MySql.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.MySql/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.MySql/vpre) |
| AvantiPoint.Packages.Database.PostgreSql | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.PostgreSql.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.PostgreSql/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.PostgreSql/vpre) |
| AvantiPoint.Packages.Database.SqlServer | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.SqlServer.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.SqlServer/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.SqlServer/vpre) |
| AvantiPoint.Packages.Database.Sqlite | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Database.Sqlite.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.Sqlite/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Database.Sqlite/vpre) |
| AvantiPoint.Packages.Hosting | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Hosting.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Hosting/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Hosting/vpre) |
| AvantiPoint.Packages.Protocol | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Protocol.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Protocol/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Protocol/vpre) |
| AvantiPoint.Packages.Registry.Npm | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Registry.Npm.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Registry.Npm/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Registry.Npm/vpre) |
| AvantiPoint.Packages.Registry.Oci | ![NuGet](https://img.shields.io/nuget/vpre/AvantiPoint.Packages.Registry.Oci.svg) | [![AP Feed](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Registry.Oci/vpre)](https://apinhousefeed.azurewebsites.net/shield/AvantiPoint.Packages.Registry.Oci/vpre) |
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
- **OpenFeed** — Multi-protocol open feed (NuGet + npm + OCI) with Blazor UI
- **Packages.Host** — Production host with config-driven npm/OCI surfaces
- **AuthenticatedFeed** — NuGet-only secured feed with authentication and callbacks

For detailed documentation, including setup guides, configuration options, and advanced features, visit the [documentation site](https://avantipoint.github.io/avantipoint.packages/). Multi-feed topics: [npm registry](docs/docs/feeds/npm-registry.md), [multi-feed UI](docs/docs/feeds/multi-feed-ui.md).

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

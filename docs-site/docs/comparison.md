---
id: comparison
title: Comparison with Other NuGet Servers
sidebar_label: Comparison
sidebar_position: 2
---

# Comparison with Other NuGet Servers

This page compares AvantiPoint Packages with other popular NuGet server implementations to help you choose the right solution for your needs.

## NuGet v3 Protocol API Comparison

Based on the service index (`/v3/index.json`) endpoint, here's what each implementation exposes:

### Core Protocol Resources

| Resource Type | AvantiPoint Packages | BaGetter | NuGet.org | Description |
|--------------|---------------------|----------|-----------|-------------|
| **PackagePublish/2.0.0** | ✅ Yes | ✅ Yes | ✅ Yes | Package upload endpoint (v2 API) |
| **SymbolPackagePublish/4.9.0** | ✅ Yes | ✅ Yes | ✅ Yes | Symbol package upload |
| **SearchQueryService** | ✅ Yes (3.0/3.5.0) | ✅ Yes (3.0) | ✅ Yes (3.0/3.5.0) | Package search |
| **SearchAutocompleteService** | ✅ Yes (3.0/3.5.0) | ✅ Yes (3.0) | ✅ Yes (3.0/3.5.0) | Package ID/version autocomplete |
| **RegistrationsBaseUrl** | ✅ Yes | ✅ Yes | ✅ Yes | Package metadata (catalog) |
| **PackageBaseAddress/3.0.0** | ✅ Yes | ✅ Yes | ✅ Yes | Package download (.nupkg) |

### Advanced Protocol Resources

| Resource Type | AvantiPoint Packages | BaGetter | NuGet.org | Notes |
|--------------|---------------------|----------|-----------|-------|
| **RegistrationsBaseUrl/3.4.0** | ✅ Yes (gzip SemVer1) | ❌ No | ✅ Yes (gzip) | Compressed registration data |
| **RegistrationsBaseUrl/3.6.0** | ✅ Yes (gzip SemVer2) | ❌ No | ✅ Yes (gzip) | Compressed with SemVer2 support |
| **RegistrationsBaseUrl/Versioned** | ✅ Yes (client 4.3.0+) | ❌ No | ✅ Yes (client 4.3.0+) | Versioned compressed registrations |
| **ReadmeUriTemplate/6.13.0** | ✅ Yes | ❌ No | ✅ Yes | Package README files |
| **VulnerabilityInfo/6.7.0** | ✅ Yes | ❌ No | ✅ Yes | Known package vulnerabilities |
| **RepositorySignatures/5.0.0** | ✅ Yes | ❌ No | ✅ Yes | Repository signing certificates |

### NuGet.org Exclusive Resources

These resources are unique to NuGet.org and not typically needed for private feeds:

- **Catalog/3.0.0** - Full package event catalog (not needed for private feeds)
- **ReportAbuseUriTemplate** - Package abuse reporting (gallery-specific)
- **PackageDetailsUriTemplate** - Web gallery package page links
- **OwnerDetailsUriTemplate** - Package owner profile links
- **SearchGalleryQueryService** - Gallery-specific search
- **LegacyGallery/2.0.0** - v2 OData feed (legacy)

### Key Differences

**AvantiPoint Packages advantages:**
- ✅ **Vulnerability awareness** - Clients can discover known vulnerabilities (configurable)
- ✅ **Repository signatures** - Supports certificate-based package signing verification
- ✅ **README support** - Direct README.md file access via URI template
- ✅ **Gzip compression** - Efficient registration data transfer (3.4.0/3.6.0)
- ✅ **Advanced search** - SearchQueryService/3.5.0 with package type filtering
- ✅ **Client version targeting** - Versioned resources for optimal client compatibility
- ✅ **More NuGet features out of the box** - With roughly the same configuration effort as BaGetter, clients see richer protocol support (README, vulnerabilities, signing)

**BaGetter advantages:**
- ✅ **Straightforward core feed** - Focused on the essential v3 endpoints
- ✅ **Lightweight** - Fewer protocol features if you don't need READMEs, vulnerabilities, or signing
- ✅ **Wide compatibility** - Covers core v3 protocol requirements

**When protocol features matter:**
- **Use AvantiPoint Packages** when you want a private feed that stays closely aligned with NuGet.org’s latest protocol features (vulnerabilities, repository signing, READMEs, and more)
- **Use BaGetter** if you need a simple, reliable v3 feed without advanced features
- **Use NuGet.org** for public packages with gallery integration and abuse reporting

### What This Means for Package Consumers

#### Vulnerability Scanning

When NuGet tooling tries to use vulnerability information:

```pwsh
dotnet restore

# or
dotnet list package --vulnerable
```

- **AvantiPoint Packages / NuGet.org**:  
  - Vulnerability data is available when the client asks for it.  
  - No extra warnings – the vulnerability endpoint is present, so restore and build logs stay clean.
- **BaGetter**:  
  - The client cannot fetch vulnerability data from the feed.  
  - The NuGet client emits repeated warnings in restore/build logs because the vulnerability endpoint is missing.

#### Package READMEs

How READMEs show up in clients (Visual Studio, `dotnet` CLI, NuGet Package Explorer):

- **AvantiPoint Packages / NuGet.org**: READMEs are exposed via the API and can be rendered directly in tooling.
- **BaGetter**: READMEs are not exposed via the API; you must download and inspect the `.nupkg` to see them.

#### Repository Signing

Trust and tamper detection for packages:

- **AvantiPoint Packages / NuGet.org**: Clients can see that packages are signed by the repository owner, improving trust and helping detect tampering.
- **BaGetter**: No repository signing metadata is exposed, so clients cannot verify repository-level signatures.

#### Compressed Registrations

Impact on restore performance and bandwidth:

- **AvantiPoint Packages**: Uses gzip-compressed registrations, making metadata responses significantly smaller (faster restores, less bandwidth).
- **BaGetter**: Returns uncompressed JSON registration data, which increases payload size and network usage.

### Service Index Response Size

Real-world comparison of service index responses:

| Feed | Resources | Response Size | Notable Features |
|------|-----------|---------------|------------------|
| **NuGet.org** | 42 resources | ~8.5 KB | Full gallery, catalog, multiple regions |
| **AvantiPoint Packages** | 20 resources | ~2.1 KB | Vulnerability, signing, README, compression |
| **BaGetter** | 12 resources | ~1.2 KB | Core v3 protocol only |

## Quick Comparison Table

| Feature | AvantiPoint Packages | BaGetter | BaGet | NuGet.Server |
|---------|---------------------|----------|-------|--------------|
| **Status** | ✅ Actively Maintained | ✅ Actively Maintained | ⚠️ Minimal Activity Since 2021 | ⚠️ Legacy |
| **Target Framework** | .NET 10.0 | .NET 9.0 | .NET Core 3.1 | .NET Framework |
| **Cross-Platform** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ Windows Only |
| **NuGet v3 API** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No (v2 only) |
| **Authentication** | ✅ Advanced (pluggable) | ⚠️ Basic | ⚠️ Basic | ❌ No |
| **Authorization** | ✅ Fine-grained per-package | ❌ No | ❌ No | ❌ No |
| **Event Callbacks** | ✅ Upload/Download/Symbol | ❌ No | ❌ No | ❌ No |
| **Repository Signatures Resource** | ✅ Yes | ❌ No | ❌ No | ❌ No |
| **Vulnerability Info Resource** | ✅ Yes | ❌ No | ❌ No | ❌ No |
| **Version Badges (Shields)** | ✅ Built-in | ❌ No | ❌ No | ❌ No |
| **Cloud Storage** | Azure Blob, AWS S3, S3-compatible (MinIO, Spaces, Wasabi, etc.), File System | Azure, AWS, GCP, Alibaba, File System | Azure, AWS, GCP, Alibaba, File System | File System Only |
| **Databases** | SQL Server, SQLite | SQL Server, SQLite, MySQL, PostgreSQL | SQL Server, SQLite, MySQL, PostgreSQL | File System |
| **Docker Support** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **ARM Support** | ✅ Yes | ✅ Yes | ❌ No | ❌ No |
| **Symbol Server** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **Read-Through Cache** | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| **Performance Optimizations** | ✅ Database views, indexes | ⚠️ Basic | ⚠️ Basic | ❌ Limited |
| **Package Size Limit** | Host-configured (Kestrel/IIS/proxy) | Host-configured | Host-configured | Limited by IIS |

## Detailed Comparisons

### AvantiPoint Packages vs BaGetter

**BaGetter** is the official community-maintained fork of BaGet. It targets .NET 9.0 and adds ARM support, making it an excellent general-purpose NuGet server.

**Why Choose AvantiPoint Packages:**
- **Richer NuGet experience by default**: With similar setup effort, you get READMEs, vulnerability info, and repository signing surfaced to clients out of the box
- **Documented S3-compatible storage**: First-class docs and examples for common S3-compatible providers (MinIO, LocalStack, DigitalOcean Spaces, Wasabi, Backblaze B2, Alibaba OSS, and more)
- **Advanced Authentication (opt‑in)**: Pluggable authentication system via `IPackageAuthenticationService` allows integration with any identity provider
- **Fine-Grained Authorization (opt‑in)**: Control access at the package level based on user licenses, subscriptions, or roles
- **Event Lifecycle Hooks (opt‑in)**: React to uploads/downloads with `INuGetFeedActionHandler` for:
  - Email notifications
  - Usage tracking and analytics
  - Security monitoring (new IPs, unusual patterns)
  - Custom business logic and compliance checks
- **Performance Optimized**: Designed for fast restores under CI/CD load while still feeling simple to run day‑to‑day
- **Modern .NET**: Targets .NET 10.0 for latest runtime features and performance improvements
- **Commercial Use Cases**: Built specifically for enterprise teams, component vendors, and SaaS platforms

**Why Choose BaGetter:**
- Great fit when you explicitly only want the core v3 feed features
- PostgreSQL support (AvantiPoint Packages currently supports SQL Server and SQLite)
- Community-driven with broad compatibility goals

**Migration Path:** AvantiPoint Packages is based on BaGet's architecture, so migration from BaGetter is straightforward. You primarily need to implement your authentication and callback handlers.

---

### AvantiPoint Packages vs BaGet

**BaGet** is the original lightweight NuGet server created by Loic Sharma. It's the foundation both AvantiPoint Packages and BaGetter are built upon.

**Why Choose AvantiPoint Packages:**
- **Active Development**: BaGet's last release was v0.4.0-preview2 in September 2021. AvantiPoint Packages is actively maintained with regular updates
- **Modern Framework**: Targets .NET 10.0 vs BaGet's .NET Core 3.1 (out of support since December 2022)
- **Security Updates**: Receives ongoing security patches aligned with current .NET releases
- **Enterprise Features**: Authentication, authorization, and event callbacks not present in BaGet
- **Production Proven**: Powers multiple commercial feeds including SponsorConnect and Prism Library Commercial Plus

**Why Choose BaGet:**
- Original reference implementation
- Extremely simple deployment for basic use cases
- Minimal configuration required

**Migration Path:** Since AvantiPoint Packages evolved from BaGet, database schemas and storage formats are compatible. The primary additions are the authentication and callback interfaces.

---

### AvantiPoint Packages vs NuGet.Server

**NuGet.Server** is Microsoft's legacy standalone NuGet server package. It's no longer actively developed.

**Why Choose AvantiPoint Packages:**
- **Cross-Platform**: Runs on Windows, macOS, and Linux (NuGet.Server is Windows-only)
- **Modern APIs**: Supports NuGet v3 protocol (NuGet.Server only supports v2)
- **Scalable**: Database-backed with cloud storage support (NuGet.Server uses file system only)
- **Well Maintained**: Active development and community support
- **Better Performance**: Database indexing and caching vs file system scanning
- **Symbol Server**: Full support for PDB/symbol packages
- **Docker Ready**: Container images available for easy deployment

**Why Choose NuGet.Server:**
- Extreme simplicity for very small internal teams
- Runs directly in IIS without separate hosting
- No external dependencies (database, cloud storage)

**Migration Path:** AvantiPoint Packages includes tools and guidance for importing packages from NuGet.Server's file-based storage.

---

## Use Case Recommendations

### Choose AvantiPoint Packages if you need:
- ✅ **Enterprise deployment** with user authentication and per-package authorization
- ✅ **Commercial package distribution** (paid subscriptions, licensed components)
- ✅ **Event tracking and monitoring** (usage analytics, security alerts, compliance)
- ✅ **Integration with existing identity systems** (Active Directory, OAuth, custom auth)
- ✅ **Multi-tenant support** (different packages for different customers/licenses)
- ✅ **Production-grade performance** for CI-heavy workloads
- ✅ **Latest .NET features** and long-term support

### Choose BaGetter if you need:
- ✅ A simple, open NuGet feed for your team
- ✅ PostgreSQL database support
- ✅ Basic read-through caching from NuGet.org

### Choose BaGet if you:
- ✅ Want the original reference implementation
- ✅ Need minimal configuration and setup
- ✅ Are okay with .NET Core 3.1 (no longer supported)

### Avoid NuGet.Server if you:
- ❌ Need cross-platform support
- ❌ Want modern NuGet v3 APIs
- ❌ Require scalability beyond a handful of packages
- ❌ Need authenticated access

---

## Attribution

AvantiPoint Packages is based on the excellent work by [Loic Sharma](https://github.com/loic-sharma) and the [BaGet project](https://github.com/loic-sharma/BaGet). We're grateful for the solid foundation and architecture that made this possible.

BaGetter continues the community-driven evolution of BaGet with broad compatibility goals. AvantiPoint Packages takes a different direction, focusing on advanced authentication, authorization, and enterprise integration scenarios.

---

## Feature Matrix

### Authentication & Authorization

| Feature | AvantiPoint Packages | BaGetter | BaGet | NuGet.Server |
|---------|---------------------|----------|-------|--------------|
| API Key Authentication | ✅ Pluggable | ✅ Basic | ✅ Basic | ❌ No |
| Basic Auth (Consumer) | ✅ Pluggable | ⚠️ Via Proxy | ⚠️ Via Proxy | ❌ No |
| Custom Auth Provider | ✅ `IPackageAuthenticationService` | ❌ No | ❌ No | ❌ No |
| Per-Package Authorization | ✅ Yes | ❌ No | ❌ No | ❌ No |
| Role-Based Access | ✅ Publisher/Consumer roles | ❌ No | ❌ No | ❌ No |
| Token Expiration | ✅ User-controlled | ⚠️ Via Proxy | ⚠️ Via Proxy | ❌ No |

### Event System

| Feature | AvantiPoint Packages | BaGetter | BaGet | NuGet.Server |
|---------|---------------------|----------|-------|--------------|
| Upload Events | ✅ `INuGetFeedActionHandler` | ❌ No | ❌ No | ❌ No |
| Download Events | ✅ `INuGetFeedActionHandler` | ❌ No | ❌ No | ❌ No |
| Symbol Upload Events | ✅ Yes | ❌ No | ❌ No | ❌ No |
| Symbol Download Events | ✅ Yes | ❌ No | ❌ No | ❌ No |
| Custom Metadata | ✅ Via callbacks | ❌ No | ❌ No | ❌ No |
| Vulnerability Sync | ✅ Included resource | ❌ No | ❌ No | ❌ No |
| Repository Signatures | ✅ Resource + fingerprints | ❌ No | ❌ No | ❌ No |
| Version Badges | ✅ Built-in shields | ❌ No | ❌ No | ❌ No |

### Storage & Databases

| Feature | AvantiPoint Packages | BaGetter | BaGet | NuGet.Server |
|---------|---------------------|----------|-------|--------------|
| File System | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes |
| Azure Blob Storage | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| AWS S3 | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| Google Cloud Storage | ❌ Not yet | ✅ Yes | ✅ Yes | ❌ No |
| Alibaba Cloud OSS (native SDK) | ❌ Not yet | ✅ Yes | ✅ Yes | ❌ No |
| SQL Server | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| SQLite | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| MySQL | ❌ Not yet | ✅ Yes | ✅ Yes | ❌ No |
| PostgreSQL | ❌ Not yet | ✅ Yes | ✅ Yes | ❌ No |

### Performance Features

| Feature | AvantiPoint Packages | BaGetter | BaGet | NuGet.Server |
|---------|---------------------|----------|-------|--------------|
| Database Views | ✅ Download counts, latest versions | ❌ No | ❌ No | ❌ No |
| Optimized Indexes | ✅ Yes | ⚠️ Basic | ⚠️ Basic | ❌ No |
| Query Batching Guidance | ✅ Documented patterns | ❌ No | ❌ No | ❌ No |
| Read-Through Cache | ✅ Yes | ✅ Yes | ✅ Yes | ❌ No |
| Package Search | ✅ Optimized views | ✅ Yes | ✅ Yes | ⚠️ File scan |
| Signed Package Discovery | ✅ Via signatures resource | ❌ No | ❌ No | ❌ No |
| Vulnerability Awareness | ✅ Integrated resource | ❌ No | ❌ No | ❌ No |
| Gzip Registration Data | ✅ SemVer1 + SemVer2 | ❌ No | ❌ No | ❌ No |

### NuGet v3 Protocol Resources

| Resource | AvantiPoint Packages | BaGetter | BaGet | NuGet.org | Purpose |
|----------|---------------------|----------|-------|-----------|---------|
| **SearchQueryService/3.5.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Package type filtering |
| **SearchAutocompleteService/3.5.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Enhanced autocomplete |
| **RegistrationsBaseUrl/3.4.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Gzip metadata (SemVer1) |
| **RegistrationsBaseUrl/3.6.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Gzip metadata (SemVer2) |
| **RegistrationsBaseUrl/Versioned** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Client-versioned metadata |
| **ReadmeUriTemplate/6.13.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Package README access |
| **VulnerabilityInfo/6.7.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Known vulnerabilities |
| **RepositorySignatures/5.0.0** | ✅ Yes | ❌ No | ❌ No | ✅ Yes | Signing certificates |
| **PackagePublish/2.0.0** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | Upload endpoint |
| **SymbolPackagePublish/4.9.0** | ✅ Yes | ✅ Yes | ✅ Yes | ✅ Yes | Symbol upload |

### Client Feature Support

| Client Feature | AvantiPoint Packages | BaGetter | NuGet.org | Notes |
|----------------|---------------------|----------|-----------|-------|
| Visual Studio Package Manager | ✅ Full | ✅ Full | ✅ Full | All feeds work |
| `dotnet restore` | ✅ Full | ⚠️ Works, but emits vulnerability-endpoint warnings | ✅ Full | BaGetter is missing the VulnerabilityInfo resource |
| `dotnet list package --vulnerable` | ✅ Yes | ❌ No | ✅ Yes | Requires VulnerabilityInfo |
| README.md display | ✅ Yes | ❌ No | ✅ Yes | Requires ReadmeUriTemplate |
| Repository signature verification | ✅ Yes | ❌ No | ✅ Yes | Requires RepositorySignatures |
| Package type filtering in search | ✅ Yes | ❌ No | ✅ Yes | Requires SearchQueryService/3.5.0 |
| Compressed metadata (bandwidth savings) | ✅ Yes | ❌ No | ✅ Yes | Requires RegistrationsBaseUrl/3.4.0+ |

---

## Quick Decision Guide

**Choose AvantiPoint Packages if:**
- 🎯 You want NuGet.org-level protocol features (vulnerabilities, signatures, READMEs)
- 🔐 You require advanced authentication and authorization
- 📊 You want event tracking, callbacks, and analytics
- 🏢 You're building a commercial package distribution platform
- ⚡ You want production-grade performance with database optimizations
- 📦 You want ~70% bandwidth savings with gzip compression

**Choose BaGetter if:**
- 🚀 You want a simple, lightweight feed for your team
- 🐘 You need PostgreSQL database support
- 🤖 You prefer community-driven open source
- 💻 Core v3 protocol features are sufficient
- 📦 You don't need vulnerability tracking or signing

**Choose NuGet.org if:**
- 🌍 You're publishing public, open-source packages
- 👥 You want community discovery and package gallery
- 📈 You need usage statistics and download counts
- 🛡️ You want Microsoft-hosted infrastructure

## Getting Help

- **AvantiPoint Packages**: [GitHub Issues](https://github.com/AvantiPoint/avantipoint.packages/issues)
- **BaGetter**: [Discord](https://discord.gg/XsAmm6f2hZ) | [GitHub](https://github.com/bagetter/BaGetter)
- **BaGet**: [Discord](https://discord.gg/MWbhpf66mk) | [GitHub](https://github.com/loic-sharma/BaGet)
- **NuGet.Server**: [NuGet Gallery Issues](https://github.com/nuget/NuGetGallery/issues)

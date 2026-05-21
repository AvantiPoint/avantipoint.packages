# Multi-feed UI components

Parent epic: [#557 — Unified Multi-Feed Platform](https://github.com/AvantiPoint/avantipoint.packages/issues/557).

Today **`AvantiPoint.Packages.UI.Razor`** provides a NuGet-focused operator and consumer experience:

| Component | Purpose |
|-----------|---------|
| `FeedInfo` | NuGet feed URL, `dotnet nuget` / `NuGet.Config` snippets |
| `PackageSearch` | Full-text search, filters, pagination |
| `PackageDetail` | Versions, README, dependencies, install commands |

The **OpenFeed** sample wires these for a NuGet-only host. Multi-feed hosts need **parallel npm and OCI experiences** on the same public origin, using `IFeedRegistry` / `IPublicBaseUrlProvider` for correct URLs behind reverse proxies.

## Design principles

1. **Protocol isolation** — npm and OCI UI must not depend on NuGet `Package` entities or `INuGetSearchService`.
2. **Surface-aware URLs** — components derive registry roots from `IFeedRegistry` + `IPublicBaseUrlProvider` (not hard-coded `/v3` or `/npm`).
3. **Reuse patterns, not domain models** — mirror NuGet component structure (connection card → browse → detail) with protocol-specific services.
4. **Optional surfaces** — navigation and pages hide when `UseNpm()` / `UseOci*` is not registered.
5. **Align with #536** — Blazor/Razor first; React parity tracked separately.

## npm UI (`FeedProtocol.Npm`)

**Registry root:** `{origin}/npm/` (from surface `RoutePrefix` via `IPublicBaseUrlProvider`).

### Components

| Component | NuGet analogue | Responsibility |
|-----------|----------------|----------------|
| `NpmFeedInfo` | `FeedInfo` | Registry URL, `.npmrc`, `npm config set registry`, auth hints |
| `NpmPackageSearch` | `PackageSearch` | DB-backed browse/search |
| `NpmPackageDetail` | `PackageDetail` | Versions, dist-tags, install commands |

### Services

- `INpmPackageBrowseService` — list/search packages for UI (uses `NpmPackage` / `NpmVersion` entities).

## OCI UI (`FeedProtocol.Oci`)

**Registry roots:** `{origin}/v2/` (default) or `{origin}/{segment}/v2/` (named).

OCI UI components are tracked in #601 and depend on #559 (OCI registry MVP).

## Multi-surface shell

`MultiFeedNavigation` reads `IFeedRegistry` at render time and shows tabs only for registered surfaces:

- **NuGet** — `/`, `/feed`, `/packages/{id}`
- **npm** — `/npm`, `/npm/feed`, `/npm/packages/{name}`
- **OCI** — `/oci`, `/oci/{segment}` (when registered)

Sample hosts: **OpenFeed** (reference), **Packages.Server** when `UseNuGetUI` is enabled.

## Tracking issues

- #599 — Phase 6 parent
- #600 — npm UI
- #601 — OCI UI
- #602 — Host shell (OpenFeed integration)

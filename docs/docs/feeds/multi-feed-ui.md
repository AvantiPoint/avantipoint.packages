# Multi-feed UI components

Parent epic: [#557 — Unified Multi-Feed Platform](https://github.com/AvantiPoint/avantipoint.packages/issues/557).

Today **`AvantiPoint.Packages.UI.Razor`** provides a NuGet-focused operator and consumer experience:

| Component | Purpose |
|-----------|---------|
| `FeedInfo` | NuGet feed URL, `dotnet nuget` / `NuGet.Config` snippets |
| `PackageSearch` | Full-text search, filters, pagination |
| `PackageDetail` | Versions, README, dependencies, install commands |

The **OpenFeed** sample wires these for a NuGet-only host. Multi-feed hosts need **parallel npm and OCI experiences** on the same public origin, using `SurfaceContext` / `IPublicBaseUrlProvider` for correct URLs behind reverse proxies.

## Design principles

1. **Protocol isolation** — npm and OCI UI must not depend on NuGet `Package` entities or `INuGetSearchService`.
2. **Surface-aware URLs** — components derive registry roots from `ISurfaceContextAccessor` + `IPublicBaseUrlProvider` (not hard-coded `/v3` or `/npm`).
3. **Reuse patterns, not domain models** — mirror NuGet component structure (connection card → browse → detail) with protocol-specific services.
4. **Optional surfaces** — components no-op or hide when `UseNpm()` / `UseOci*` is not registered.
5. **Align with #536** — Blazor/Razor first; React parity tracked separately.

## npm UI (`FeedProtocol.Npm`)

**Registry root:** `{origin}/npm/` (from surface `PublicBaseUrl`).

### Components (planned)

| Component | NuGet analogue | Responsibility |
|-----------|----------------|----------------|
| `NpmFeedInfo` | `FeedInfo` | Registry URL, `.npmrc`, `npm config set registry`, auth (`NPM_TOKEN` / Bearer), scoped publish hints |
| `NpmPackageSearch` | `PackageSearch` | Browse/search packages (DB-backed list initially; `/-/v1/search` when implemented) |
| `NpmPackageDetail` | `PackageDetail` | Packument metadata, versions, dist-tags, `npm install` / `pnpm add` / `yarn add` commands |

### Services

- `INpmPackageBrowseService` — list/search packages for UI (not the registry search API alone).
- Uses feed DB (`NpmPackage` / `NpmVersion`) or future npm search endpoint.

### Acceptance (npm UI v1)

- [ ] Operator can copy registry URL and auth setup for CI
- [ ] Consumer can find a package and see install commands for a selected version/dist-tag
- [ ] Scoped package names display and link correctly (`@scope/pkg`)
- [ ] Works on same host as NuGet (OpenFeed or Server with `UseNpm()`)

## OCI UI (`FeedProtocol.Oci`)

**Registry roots:** `{origin}/v2/` (default) or `{origin}/{segment}/v2/` (named).

### Components (planned)

| Component | NuGet analogue | Responsibility |
|-----------|----------------|----------------|
| `OciFeedInfo` | `FeedInfo` | `docker login`, `helm registry login`, ORAS login hints; per-segment base URL; auth realm note |
| `OciRepositoryCatalog` | `PackageSearch` | Repository list (`/v2/_catalog` when enabled) + tag list per repo |
| `OciArtifactDetail` | `PackageDetail` | Manifest media type, platforms, digest, pull commands (`docker pull`, `helm pull oci://`, `oras pull`) |

### Services

- `IOciRepositoryBrowseService` — catalog + tags (wraps Distribution API).
- Surface parameter: default vs named segment (`OciSegment`).

### Acceptance (OCI UI v1)

- [ ] Operator can copy correct login/pull URLs for default and named segments
- [ ] Consumer can browse repositories and tags (when catalog API enabled)
- [ ] Artifact detail shows pull commands appropriate to kind (image vs Helm vs generic)
- [ ] Coexists with NuGet/npm routes on unified host

## Multi-surface shell

When a host registers more than one surface, provide a lightweight **surface switcher** (tabs or nav):

- **NuGet** — existing components
- **npm** — npm components
- **OCI** — list registered segments (`default`, `helm`, `docker`, …) then OCI components

Sample: extend **OpenFeed** (or `Packages.Server` UI) with `/`, `/npm`, `/oci`, `/oci/{segment}`.

## Project layout (proposed)

```
src/AvantiPoint.Packages.UI.Razor/          # existing NuGet components
src/AvantiPoint.Packages.UI.Npm.Razor/    # npm components (new)
src/AvantiPoint.Packages.UI.Oci.Razor/    # OCI components (new)
```

Alternative: single RCL with folders `Components/NuGet`, `Components/Npm`, `Components/Oci` — decision in implementation issue.

## Dependencies

| UI work | Blocked by |
|---------|------------|
| npm UI | #561 (npm registry MVP) — **done** |
| OCI UI | #559 (OCI registry MVP) |
| Surface shell | Feed.Platform surface registry (Phase 0) — **done** |

## Out of scope (UI v1)

- Cross-feed unified search
- npm provenance / Sigstore UI
- OCI garbage-collection operator console (Phase 5 / #563)
- React components (#536) — follow-on

## Tracking issues

See GitHub issues created under #557 (Phase 6 — Multi-feed UI).

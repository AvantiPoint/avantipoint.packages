# Two-host mirror integration tests

Issue [#581](https://github.com/AvantiPoint/avantipoint.packages/issues/581) validates package **origin** and **caching strategy** behavior end-to-end.

## Topology

```text
  ┌─────────────────────────┐         HTTP (loopback)        ┌──────────────────────────┐
  │  UpstreamOpenFeedHost   │  ◄───────────────────────────  │   FeedUnderTestHost      │
  │  (OpenFeed sample stack)│                                │  (IntegrationTestApi)    │
  │  Kestrel + Sqlite + FS  │                                │  + PackageSource row     │
  └─────────────────────────┘                                └──────────────────────────┘
```

1. **Host A (upstream)** — `UpstreamOpenFeedHost` starts the same NuGet API stack as the [OpenFeed](../../../samples/OpenFeed) sample on a real TCP port (`127.0.0.1`). Packages are published with `TestPackageBuilder` so `.nupkg` content exists on disk.

2. **Host B (feed under test)** — `FeedUnderTestHost` starts the minimal `IntegrationTestApi` host, seeds a `PackageSource` pointing at Host A’s `/v3/index.json`, and configures `Search.IncludeMirroredPackages` / `CachingStrategy` per test.

`MirrorService` creates its own `HttpClient` instances (not `WebApplicationFactory`’s in-memory server), so both hosts must use **Kestrel on loopback** — the same pattern as `FeedTestServerHost` in registry tests.

## Test categories

| Tests | Attribute | Upstream |
|-------|-----------|----------|
| `PackageOriginIntegrationTests` | `[Fact]` | OpenFeed host (CI-safe) |
| `NuGetOrgCachingIntegrationTests` | `[ExternalNetworkFact]` | nuget.org (skipped when offline) |

## Assertions

- **Database** — `FeedUnderTestHost.FindPackageAsync` / `IContext.Packages`
- **Filesystem** — `*.nupkg` count under `StoragePath`
- **HTTP search** — `GET /v3/search?q=...`

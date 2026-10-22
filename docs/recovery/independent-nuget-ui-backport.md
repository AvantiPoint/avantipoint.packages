# Independent NuGet UI backport

## Source separation

Proposed base: master `75089938f119cbe7dccf0ffd99c38ea60faa6dbe`, including dependency consolidation #735. This is a new isolated worktree/branch, not a retarget of #744 and not a merge/cherry-pick of restoration #738. No restoration commit is in its ancestry.

The port uses master’s existing `PackageInfoCollection`, `PackageDependencyInfo.IsLocalDependency`, `IPackageMetadataService` and `IPackageContentService` contracts. It reuses the small new text-cache and license-label helpers from the ordinary UI work, while adapting the component and scoped CSS to master. No Core, Protocol, registry, authorization policy, database provider, migration, central dependency or deployment workflow file is changed. Existing known backend risks are neither removed nor claimed fixed.

Runtime paths changed:

- `src/AvantiPoint.Packages.UI.Razor/Components/PackageDetail.razor`, its new partial `.razor.cs`, and its existing scoped `.razor.css`.
- `Components/DependencyRow.razor`: default rendering uses the existing local-dependency flag instead of issuing a NuGet.org HEAD request for every row. Optional caller-provided URL resolution remains supported, deduplicated per row input and guarded against late results.
- `Services/PackageDetailTextCache.cs`, `Services/PackageLicenseDisplay.cs`, and `wwwroot/js/ap-package-detail.js`.
- The Host package-details Razor Page/model: serializable route/version/tab parameters replace the existing prerender delegate parameter; request cancellation is forwarded.
- A single base-URI element in the existing Host layout is needed for correct Blazor and asset URL resolution. No feed navigation, sign-in, role or anonymous-access policy is changed.
- The UI project adds test-only internals visibility. Focused tests and a generated loopback browser fixture complete the patch.

The native selector, current-version metadata, lightweight Versions tab, bounded optional README loading, accessible custom-license modal and header labels can therefore be built independently on master. Text caching remains component-owned (four entries, one active reader, at most one adjacent README warm-up). Anonymous/uninitialized UI contexts continue through the existing protocol authorization boundary; in-process text is used only for a signed-in circuit. No credentials are created or copied.

## What remains separate

#744 stays at its tested restored-source head and keeps the complete newer portal experience. Master has no `IPackageDiffService`, package Diff model/service graph, `FeedUiPath`, Tailwind input/build assets, or the newer package origin/framework/deprecation metadata used by that component. Copying its entire component onto master would require those additional files or remove/misrepresent capabilities. This backport does not import that graph.

#739's autocomplete correction targets the restored search component; master's plain input does not contain that picker. It is not a safe direct transplant or an independently verified fix for a picker absent from master. #740 changes portal authorization and monitoring visibility and is not part of this ordinary UI slice. Vulnerability inventory, OCI ownership/security remediation, schema reconciliation and all restoration-only backend changes are excluded.

This PR is partial progress for #741/#742, not evidence that the currently deployed portal has been fixed. Do not close those deployed-behavior issues solely because this source backport merges. #743 feed freshness remains a separate read-only diagnosis.

## Deployment provenance and exact gate

The preserved restoration provenance matches eight Host/UI embedded-PDB document checksums to the preserved source. It expressly does not attest all files of the deployed build and does not include its runtime configuration. Master is older than that preserved portal and lacks its Diff/framework/origin UI. Deploying a newly built master Host as a substitute for that newer portal could therefore remove supported features and alter unrelated runtime paths.

This cloud workspace exposes no Azure deployment connector, Azure CLI, authenticated Kubernetes/AWS context or cached deployed container image. GitHub deployment records are not an endpoint supported by the available read connector. No Azure access, credentials, grants or image registry settings were changed. A production deployment was not attempted.

To qualify an independent Azure candidate, the existing deployment owner must supply or inspect the current immutable image digest, exact source/build manifest and matching runtime assembly/reference provenance, plus the currently deployed immutable rollback image. Apply the full UI-only #744 delta to that proven UI/runtime baseline (or prove ABI/parameter compatibility of a narrower artifact update), while leaving backend assemblies/schema/security behavior unchanged. The master's component is not a drop-in replacement for a newer Host that passes Diff-specific parameters. A complete master rebuild is not evidence of that compatibility.

This is a concrete production provenance/compatibility gate, not a requirement to finish the blocked OCI repair before any unrelated source change can land. The security owner remains stopped. No executor switch, blocked operation retry, protection removal, security fix claim, production migration or customer-data mutation is part of this work.

## Validation

Run the master's normal solution CI and these local checks before merging this independent source patch:

```sh
dotnet build tests/AvantiPoint.Packages.UI.Tests/AvantiPoint.Packages.UI.Tests.csproj -m:1 -p:GeneratePackageOnBuild=false
dotnet tests/AvantiPoint.Packages.UI.Tests/bin/Debug/net10.0/AvantiPoint.Packages.UI.Tests.dll
dotnet build src/host/AvantiPoint.Packages.Host/AvantiPoint.Packages.Host.csproj -m:1 -p:GeneratePackageOnBuild=false
dotnet run --project tests/AvantiPoint.Packages.UI.BrowserFixture --urls http://127.0.0.1:5188
python scripts/check-independent-nuget-ui.py
```

The fixture uses generated package metadata and fake license text. Browser checks cover pointer/keyboard native selection, repeated tabs with no document reload, rapid versions, history, interrupted package navigation, encoded license text, modal Escape/focus return, known MIT links and narrow layout. These checks do not measure the production feed. The earlier #744 synthetic latency comparison applies to its restored component, not to master’s pre-existing local tab buttons.

Existing source files retain their tracked CRLF convention; whitespace validation uses `git -c core.whitespace=cr-at-eol diff --check`. New files use LF. Central dependencies remain byte-identical to #735. Existing dependency audit warnings are unchanged.

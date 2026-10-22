# NuGet package-details UI validation

Tracking: #741 (navigation/loading), #742 (header/licenses), #743 (reported latest-version discrepancy).

## Integration and release gate

This UI-only branch starts at restoration PR #738 head `db1348a6dc7bec779d710aabbab48dc4f13708b1`, which contains master `75089938f119cbe7dccf0ffd99c38ea60faa6dbe` and dependency consolidation #735. Its regular PR targets `codex/portal-source-integration-20261022` to expose only the independent UI patch. Search #739 and anonymous portal privacy #740 remain separate open dependent PRs. No other feature branch is edited or merged.

The UI implementation depends on source absent from master. Therefore this candidate must not be merged into the restoration branch or deployed while #738 remains unqualified. In particular, this patch neither resumes nor resolves the separate blocked OCI remediation, and includes no OCI authorization, migrations, security remediation, deployment workflow, credential or production configuration changes. User deployment authority does not remove the restoration integration gate.

No deployable Azure image/digest has been built or qualified for this branch. No production release or live verification is claimed. Once the restoration owner qualifies the base, replay/retarget these UI-only commits, integrate separately qualified #739/#740 as appropriate, test the exact final source/image, and coordinate deployment and rollback with the existing release owner. Preserve the currently deployed immutable image as the rollback target before an authorized rollout. Validate the requested UI interactions against the authenticated live portal after rollout, not merely pipeline success.

## Observed causes

The unchanged restored `PackageDetail` uses full-document links for content/install tabs and version links. Each request reacquires all-version metadata in the Host and eagerly reads README storage. If the provided README is empty, the component also tries the public feed URL; component initialization runs during prerender and circuit initialization. The underlying metadata service also supports a live upstream lookup according to existing options. This patch does not change feed freshness policy or database query behavior.

The synthetic browser fixture exercises the actual Razor component, HTTP text requests, server prerender, Blazor circuit, DOM and CSS. Its deterministic 300 ms page/metadata and 700 ms text delays isolate repeated work. Before the patch, eight tab changes in this fixture made eight document requests and sixteen README HTTP requests. The fixture exercises the component HTTP fallback; the real Host normally supplies a nonempty eagerly read README and does not make those fallback HTTP calls, but still repeats its storage read and metadata lookup per document. These HTTP counts must not be presented as a production Host trace. The picker opens after the circuit is ready in this unchanged source, so a permanently broken post-initialization picker was not reproduced. Its custom click handler is unavailable before circuit initialization; the replacement uses a native select that opens without that dependency.

Header SVGs previously had no explicit dimensions. Their intrinsic browser sizes distorted the action layout. All three actions now use the same labelled control with 18 px icons, at least 44 px height and visible keyboard focus. Known SPDX identifiers/expressions use NuGet's existing license parser and standard-identifier validation. Embedded files take precedence, and custom/invalid metadata is never fabricated into an SPDX URL. Available embedded text is encoded into a native modal dialog with Escape, focus containment, focus return and a Close action.

## Measured results

Chromium on this cloud workspace, eight alternating Versions/Dependencies/Frameworks/ReadMe transitions after initial warm-up, using the same synthetic metadata/text delays:

| Measure | Unchanged `db1348a6dc` | UI patch |
| --- | ---: | ---: |
| Median transition | 1233.8 ms | 87.5 ms |
| Slowest sampled transition | 1384.0 ms | 178.7 ms |
| Full document requests | 8 | 0 |
| Additional text HTTP requests | 16 | 1 adjacent README warm-up |
| Browser JavaScript errors | 0 | 0 |

The loopback synthetic median improves by about 93%. These timings include Playwright click/actionability and browser render overhead; they are not a live-feed latency guarantee or a production database benchmark. Per-transition samples are checked in under `tests/AvantiPoint.Packages.UI.BrowserFixture/profiles/`.

The final UI suite passes 185 tests, zero failures/skips. Browser checks pass native keyboard/pointer selection, rapid/repeated versions, interruption by a different package, Back/Forward, keyboard tabs, modal Escape/focus return, encoded custom text, MIT URL, preserved authors, equal 18 px icons and narrow/dark layout. The existing dependency audit warnings for System.Security.Cryptography.Pkcs 10.0.11 remain unchanged; resolving unrelated dependency/security work is outside this patch.

## Loading and state bounds

- Keep the current package's metadata snapshot and render only the selected content panel. Versions, dependencies, frameworks and release notes need no additional metadata request.
- Read the selected version's README after the first interactive render. After 300 ms of quiet, warm at most one adjacent stored version's README. Never speculatively read archives, diffs, licenses, upstream previews or all version contents.
- Deduplicate text tasks by feed/content base, package, normalized version and asset kind. Each component owns at most four entries, with least-recent eviction and one active text reader. Foreground requests cancel unrelated speculation. Text is capped at 512 Ki characters per entry.
- Cancel obsolete consumers on version changes, and cancel retained readers on eviction, package/feed/authentication changes and disposal. Generation and diff-key checks reject late results from non-cooperative providers. Completed text tasks can be reused on repeated navigation; only one completed diff is retained.
- No static, global, browser-storage or cross-user content cache. Authentication-state notifications clear the component cache and reload through the host's existing boundary. This is not a new cross-tab/session revocation mechanism; pre-existing circuit principal refresh limitations remain.
- Preserve links for initial rendering and modified-click/new-tab behavior. Local navigation updates browser history; Back/Forward restores selected version and query/tab state. A new package navigates through the server and a new component scope.

## Reproduce

Use the repository's .NET 10 SDK and existing pinned npm dependencies:

```sh
npm ci --prefix src/AvantiPoint.Packages.UI.Razor
npm run css:build --prefix src/AvantiPoint.Packages.UI.Razor
dotnet build tests/AvantiPoint.Packages.UI.Tests/AvantiPoint.Packages.UI.Tests.csproj -m:1 -p:GeneratePackageOnBuild=false
dotnet tests/AvantiPoint.Packages.UI.Tests/bin/Debug/net10.0/AvantiPoint.Packages.UI.Tests.dll
dotnet run --project tests/AvantiPoint.Packages.UI.BrowserFixture --urls http://127.0.0.1:5187
python scripts/profile-package-detail.py --url http://127.0.0.1:5187
```

The Python script requires Playwright and Chromium (`/usr/bin/chromium`). To reproduce the baseline, copy the **unchanged** browser fixture into a detached checkout of `db1348a6dc7bec779d710aabbab48dc4f13708b1`, run it on port 5186, and pass `--baseline --url http://127.0.0.1:5186`. Do not run simultaneous builds of the same checkout. The fixture uses only generated metadata named AppPortal.Cli and Prism.Core; its license text and package versions are deliberately synthetic, not copies of private package contents. The baseline originally did not include the fixture; copying it supplies the identical harness without modifying the component under test.

## Live freshness limitation

The actual authenticated portal URL/feed response and the identity of the CLI published today were not supplied to this isolated workspace. No authenticated live registration, flat-container, database or package publication state was inspected. The fixture intentionally contains both 3.0.52 and 3.0.60 and verifies selection/state behavior only. #743 must remain open until the existing feed owner compares the actual package ID, environment, registration versions, listing/upstream state and deployed source revision. This patch does not assert that production should select 3.0.60, or attribute feed freshness to caching without evidence.


## Follow-up qualification of PR #744

The existing head `778e6d3983cdb9fd6d1b657295a72e2683dd95fc` had zero GitHub check runs, zero commit statuses and zero Actions runs. The build workflow accepts pull requests targeting `master`; this dependent PR targets the restoration branch, so the absence of hosted build checks is not a passing CI result. There were no submitted reviews or inline review threads. The automatic review summary reported failure because the account's code-review quota was exhausted. No retry, new credits, account/access change or replacement review service was requested. The summary's generic unresolved-findings flag did not include any published inline findings; it is not a completed review or approval.

Local ordinary UI review added two regression tests, both failing before correction and passing afterward. An explicit ReadMe tab now overrides an old `compare` query. Install links derive state from the current active tab instead of stale initial Diff parameters, and content links preserve the selected install option. Static prerender now announces a pending README instead of briefly claiming that no README exists. These are confined to NuGet presentation and query state.

All 185 UI tests pass after those corrections. Chromium also verifies the complete initial Diff → ReadMe → Package Manager → References sequence, preserving the install selection without document reloads. The follow-up profile (`profiles/qualification.json`) measured a 104.0 ms median and 115.2 ms maximum, zero document reloads and one bounded README warm-up over the same eight synthetic transitions. The earlier 87.5 ms sample remains historical evidence, not a production claim; timing varies across local runs.

[View the synthetic screenshot evidence](package-details-ui-fixtures/README.md). No private publication logs, package binaries, credentials or customer data are included in these artifacts.

Read-only freshness follow-up found existing publication metadata for the same package ID, but direct reads of both the documented legacy feed origin and the actual publishing origin failed at this workspace's network proxy with `Tunnel connection failed: 403 Forbidden`. This occurred before receiving a feed HTTP response; it does not establish that the feed itself returned 403 or rejected authentication. No proxy bypass or new credential/access request was attempted. Consequently the current feed version list, the reported page's selected route/feed and any response-cache headers remain unreadable here, and #743 remains unresolved. Publication details from the authorized private source were reported to the requester without copying its logs or artifacts into this public repository.

The restoration/safeguard integration hold remains intact. A conflict-free GitHub mergeability result is not qualification of the base, hosted CI, completed review or deployment authority to bypass that hold.

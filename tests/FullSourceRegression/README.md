# Referrers HTTP qualification

The component project runs 47 tests without the complete application. It links the actual authentication adapter, route parser, scoped token issuer and validator; external platform contracts/options are test-only projections.

`OciReferrersHttpTests.cs` additionally exercises the real middleware, routing, adapter, token endpoint, scoped validator, registry service, SQLite and HTTP response serialization. Anonymous pulls are explicitly disabled. Every identity and artifact is synthetic. The five cases cover the DevSpace namespace, the named Docker surface, and nested names containing route words. They assert the exact challenge scope, successful challenge-driven retry, empty OCI image-index semantics with artifactType filtering, invalid credential denial and cross-repository token denial.

The full application is deliberately outside the component recovery branch. Given a read-only checkout containing the preserved source commit, create a fresh validation copy:

```sh
python3 tests/FullSourceRegression/prepare.py /path/to/preserved-checkout /tmp/new-referrers-validation
cd /tmp/new-referrers-validation
dotnet test --project tests/AvantiPoint.Packages.Registry.Oci.Tests/AvantiPoint.Packages.Registry.Oci.Tests.csproj --configuration Release -- --no-ansi --filter-class '*OciReferrersHttpTests'
```

The preparation script archives only commit `db3c29924dbaf30da285b57877f4486964ed6c51`, overlays this branch's OCI C# sources and authentication adapter, and adds the HTTP tests. It does not copy uncommitted work or modify the original checkout. The archive's original projects, dependencies and external platform contracts remain in use.

## Results and limits

- Component baseline: 14 failures / 47 tests before the fix; 47 passed afterward, no skips.
- Five HTTP regressions: all passed with the fix. All five fail on the unchanged baseline (missing/truncated scope or route-word misrouting), and all five pass with the fix.
- Broader HTTP suite, excluding the native-toolchain class: 105 passed / 108; three existing token tests fail identically on the unchanged recovery baseline. The legacy tests send `service=test` although the resolved service is `default`; one also expects a raw API key and another expects denial instead of the scoped-token intersection's empty grant. Their assumptions belong to the earlier token change and were not altered here.
- Existing artifact-indexing, artifactType filtering and invalid-digest HTTP tests pass within those 105 successes.
- Full native Docker/Helm pull qualification and production image qualification remain separate. No production registry request was made.

The full-source project emits existing package-audit warnings for System.Security.Cryptography.Pkcs 10.0.11; dependencies were not changed by this fix.

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

## Subject-before-image ordering

`OciSubjectOrderingHttpTests` exercises actual HTTP writes and SQLite for default and named surfaces, including a nested repository name. All three cases reject a valid referrer on the unchanged service, and pass after accepting the weak subject association. The checks retain unauthorized-write denial, config/blob dependency rejection, retry deduplication, exact `OCI-Subject` responses, and repository membership isolation: a subject stored only in another repository remains unavailable in the receiving repository, while its referrers list does not acquire the receiving repository's artifact.

The [OCI Distribution push requirements](https://github.com/opencontainers/distribution-spec/blob/main/spec.md#push) explicitly require accepting an otherwise valid manifest whose subject does not yet exist in that repository. The [image manifest specification](https://github.com/opencontainers/image-spec/blob/main/manifest.md#image-manifest-property-descriptions) defines subject as a weak association. Existing per-repository referrers indexes can record that association immediately, with no new pending queue, background resolver, cross-repository lookup, or change to access grants. The subject must still have a supported SHA-256/SHA-512 digest with canonical length and lowercase hexadecimal encoding; malformed/path-bearing descriptors are rejected independently of existence. Config, layer and child-manifest dependencies retain their existing checks.

Current complete-source result: 108 passed / 111, with exactly the same three legacy token failures documented above. Component coverage is now 71 passing tests, including 11 subject-descriptor cases. Native fresh-image qualification remains a separate release gate.

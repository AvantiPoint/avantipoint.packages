# Portal source restoration baseline

This branch starts at master `443a30f9bd35c4bc2a2ffa033fba695cc087bddf` and restores an explicitly reviewed subset of committed local source `db3c29924dbaf30da285b57877f4486964ed6c51`. It does not bring that unpublished commit or its private history into GitHub ancestry. Eight cached deployed Host/UI embedded-PDB document checksums match the preserved sources, including PackageSearch.razor, Host Index/layout and feed navigation. Those file matches do not attest every file of the original build.

The selection follows project-reference closure from Host, UI.Tests, Host.Admin.Tests and Registry.Oci.Tests. Supporting Core, Protocol, feed/auth, storage, registry, administration and database schema source is included because the current portal depends on it. Existing EF migration/designer/model-snapshot C# files are schema source, not database exports; no migration was executed. An additional OpenFeed component is required by the test project closure. Unrelated local orchestration, legacy Server, documentation, root container scripts, configuration snapshots and unrelated tests are excluded. See the provenance manifest for every restored file and source checksum.

No appsettings snapshots, credentials, local WIP, keys, databases, binaries or build output were imported. The Host local UserSecretsId metadata is omitted. The source Tailwind inputs and public dependency lockfile are included; the generated `wwwroot/css/app.css` is deliberately absent. Generate it in the cloud with the scripts in `src/AvantiPoint.Packages.UI.Razor/package.json` before visual qualification. Master configuration remains in place, so this branch is a code baseline rather than an exact deployable configuration snapshot.

The restored UI intentionally still contains issues #736/#737 so the cloud owner can reproduce and fix them. No search/privacy implementation is bundled here. Preserve the existing typed query and protocol authentication behavior while adding the requested fixes. Subsequent commits carry the already qualified OCI production fixes so reconciliation cannot silently restore the earlier registry bugs.

The Directory.Packages.props changes preserve the source's known dependency baseline. Reconcile them with the separate consolidated Dependabot work instead of overwriting newer tested versions. A complete master merge requires broader compatibility review and tests; this preservation branch is not approval to publish another image.

## Initial qualification

The restored Host and its referenced UI/runtime projects compile successfully (zero errors). The baseline retains existing NuGet audit warnings for System.Security.Cryptography.Pkcs 10.0.11; coordinate upgrades with the cloud dependency owner before a future master release. The imported component harness uses the full repository central package versions, which match its qualified Microsoft.NET.Test.Sdk/xunit/Azure versions. UI baseline tests pass 165/165, Host admin/auth tests pass 261/261, and component regression tests pass 71/71, with zero skips. No assertions or application behavior were changed to mask existing bugs. The first UI test invocation lost parallel MSBuild workers; serial compilation followed by test execution succeeds. The generated CSS and production configuration are intentionally not qualification claims for this preservation branch.

One reviewed JSON test fixture, `Host.Admin.Tests/Fixtures/entra-access-profile.json`, is included because its tests require it. It contains example.com and repetitive mock GUIDs (matching the test constants), with no client credentials. It is synthetic test input, not a deployed configuration export.

## PostgreSQL identity upgrade preflight

The restored case-insensitive NuGet identity index cannot represent two package rows with the same lowercased ID, feed, and normalized version. Before an operator schedules this upgrade, use a read-only query through the existing approved database-access process:

```sql
SELECT lower("Id") AS package_id, "FeedId" AS feed_id,
       lower("Version") AS version, count(*) AS row_count,
       array_agg("Key" ORDER BY "Key") AS package_keys
FROM "Packages"
GROUP BY lower("Id"), "FeedId", lower("Version")
HAVING count(*) > 1
ORDER BY "FeedId", lower("Id"), lower("Version");
```

No returned groups means this particular collision precondition is clear. The migration repeats the check while holding a table lock, then creates the index in the migration transaction. A collision aborts the upgrade with an actionable error; it never picks a winner, merges identities, renames packages, or deletes rows or package contents. Normal driver settings may redact the error detail, so use the read-only query to obtain all affected keys rather than enabling sensitive error logging.

If groups are returned, preserve all rows and stored package bytes and hold the upgrade. An operator must determine the intended identities through the established data-resolution process before retrying. No production query, migration or data repair is performed by this source reconciliation. Already-applied migration history is not rewritten or rerun.

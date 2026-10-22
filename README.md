# OCI production-component recovery

This maintenance branch preserves only the thirty repository source files whose SHA256 checksums match the deployed OCI assembly's embedded PDB. The original source revision is `db3c29924dbaf30da285b57877f4486964ed6c51`; `provenance.json` records the component and base-image digests and source checksums.

This is a component recovery baseline, not the default master tree or a complete Packages application source release. No operator configuration, credentials, deployment state, binaries, or unrelated preserved application changes are included.

The immutable source baseline enables a narrow async-stream correction to be reviewed without publishing the broader preserved application tree. Full image qualification requires the pinned production image and its exact dependency assemblies. No candidate image or deployment is represented by this baseline.

## Async upload correction

The hotfix adds the byte-array `ReadAsync` overload to `OverlayReadStream` and `Sha256TrackingReadStream`, delegating to their existing memory-based asynchronous paths. The three existing stream classes are separated into one file each. Other recovered OCI source files remain byte-identical to the verified baseline.

The standalone regression project links the actual corrected stream source and uses Azure.Storage.Blobs 12.29.2, verified from the deployed dependency manifest. Its in-memory HTTP transport exercises the real Azure SDK's multipart uploader for both PATCH-style and completion streams with synchronous reads forbidden. It does not use credentials or contact Azure. These tests qualify the stream correction; they do not compile or qualify a complete production component/image.

The original full-source validation passed 112 OCI tests locally, with two native Docker/Helm CLI cases explicitly excluded because of the Mac daemon transport boundary. Three new regressions failed before the fix; all ten focused tests passed afterward.

## Image qualification remains required

The pinned production base is not locally cached, and registry authorization has not been retried. No candidate image, production rollout, or DevSpace publication is claimed by this source branch. A qualified image must retain the pinned base and replace only the OCI assembly, preserving assembly identity, target framework, dependency/public API compatibility, every other file hash, and application configuration. Runtime qualification must use isolated synthetic state and exercise realistic Azure uploads before an image-only rollout. Rollback must restore the original pinned base digest. The broader preserved application snapshot and migrations are outside this branch.

## Scoped OCI token negotiation

Mixed `pull,push` requests receive the intersection of their requested actions and the existing credential's authorization. Valid identities with no requested resource permission receive an empty grant; invalid credentials remain unauthorized. Artifact routes continue to check the original credential, including expiry, revocation, account policy and feed authorization.

Issued tokens are encrypted and time limited with the host's existing ASP.NET Core Data Protection provider. Each token is bound to its feed, surface, exact repository names and granted actions. Login tokens grant no artifact access; catalog access requires an explicit catalog grant. A scoped token cannot be exchanged for additional repositories, actions, feeds or a later expiry. Existing direct API-key authentication remains supported. No new credential, permission, database migration or security configuration is required.

The standalone regression project links the production token and authorization source. Its contract fixtures mirror the small external interfaces needed for compilation, with a test-only projection for route registration; they are not deployed dependencies. Full component compilation and HTTP runtime qualification must additionally use the pinned image's actual dependency assemblies. Tests use synthetic identities and an ephemeral in-memory Data Protection provider, never production keys.

## Referrers authentication challenge correction

The operation-aware platform authentication adapter omitted `/referrers/` from repository scope extraction. An unauthenticated referrers GET therefore advertised no repository scope. With scoped access tokens, following that challenge issues a login-only token, which cannot read referrers. This is a reproduced source defect consistent with the AppPortal DevSpace publisher failure, not proof of the current deployed implementation or request headers.

The adapter now advertises the exact repository's `pull` scope. Both challenge extraction and the OCI route parser recognize the terminal operation, preserving slash-separated repository names even when a segment is named `manifests`, `referrers`, `blobs`, or `tags`. Permission checks, credential validation, token grants and referrers response semantics are unchanged.

The adapter is preserved separately from the original local Git commit before the fix, with its SHA256 in `authentication-source-provenance.json`. Unlike the OCI source in `provenance.json`, it has **not** been checked against a deployed PDB. This branch now contains a narrow source change in **two assemblies**, Feed.Platform and Registry.Oci; the earlier OCI-only replacement instructions are insufficient for this correction. See [local HTTP validation and limitations](tests/FullSourceRegression/README.md).

Release steps require separate authorization: verify the deployed Feed.Platform/OCI source and exact dependencies; build both corrected assemblies against those dependencies; qualify assembly identity, public API and configuration compatibility in a pinned candidate image; run isolated authenticated referrers and native Docker pull tests; review and authorize the merge and rollout with rollback digest recorded. After rollout, an authorized publisher rerun must pass pull verification before latest promotion. Neither source CI nor local HTTP tests establish deployment or live verification. No credentials, permissions, verification gates or production state were changed.

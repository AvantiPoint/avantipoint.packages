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

# OCI production-component recovery

This maintenance branch preserves only the thirty repository source files whose SHA256 checksums match the deployed OCI assembly's embedded PDB. The original source revision is `db3c29924dbaf30da285b57877f4486964ed6c51`; `provenance.json` records the component and base-image digests and source checksums.

This is a component recovery baseline, not the default master tree or a complete Packages application source release. No operator configuration, credentials, deployment state, binaries, or unrelated preserved application changes are included.

The immutable source baseline enables a narrow async-stream correction to be reviewed without publishing the broader preserved application tree. Full image qualification requires the pinned production image and its exact dependency assemblies. No candidate image or deployment is represented by this baseline.

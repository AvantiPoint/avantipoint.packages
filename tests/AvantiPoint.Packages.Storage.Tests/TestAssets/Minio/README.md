# Local MinIO test fixture

Only `MinioS3StorageIntegrationTests` uses this image. The public `minio/minio`
image stopped being anonymously pullable; upstream now distributes OSS MinIO as
source. Build this fixture locally through Testcontainers instead of substituting
another S3 provider, skipping the roundtrip, or requiring registry credentials.
No CI workflow publishes this image, and it must not be used in production.

## Immutable inputs

- Official [RELEASE.2025-10-15T17-29-55Z](https://github.com/minio/minio/releases/tag/RELEASE.2025-10-15T17-29-55Z),
  commit `9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a`.
- The commit-addressed GitHub source archive is checked against SHA-256
  `45521908307306e925c98d629e1c17d78c8b72b6ee242b1bfb1409f7d8ee5841`.
- Docker Official Image `golang:1.24.13-alpine3.23`, manifest digest
  `sha256:8bee1901f1e530bfb4a7850aa7a479d17ae3a18beb6e09064ed54cfd245b7191`.
  `GOTOOLCHAIN=local` prevents automatic toolchain changes. `go.sum`, the public
  Go checksum database, and `go mod verify` authenticate downloaded dependencies.
  The executable is compiled from the resulting vendored dependencies.
- The runtime is `scratch`; no unpinned runtime packages are installed.

The October release fixes [CVE-2025-62506](https://github.com/minio/minio/security/advisories/GHSA-jjjj-jwhf-8rgr).
The OSS project is archived and **has later unfixed vulnerabilities**, including
[CVE-2026-41145](https://github.com/minio/minio/security/advisories/GHSA-hv4r-mvr4-25vw).
This fixture is an isolated compatibility test, not a supported or fully patched
storage service. It uses disposable synthetic data, a generated test password,
no host mounts, a non-root process, a disabled web console, and a random port
published only on the Docker host's loopback. Run it with a local Docker daemon.
Do not expose it to untrusted clients or use real credentials/data.

## Cost and cleanup

The source/dependency/build step has a 15-minute hard timeout, two Go workers,
and a 4-GiB Docker build memory limit. Image creation is cancelled after 20
minutes (including base-image download); startup is limited to two minutes.
The Docker build context contains only these three fixture files. No external
build cache is uploaded. Testcontainers disposes the test container and final
image; its Resource Reaper labels also cover the intermediate build stage.
Go caches are removed before that stage completes. CI runners are ephemeral;
only the immutable official base image may remain in a developer's Docker cache.
A timeout, checksum mismatch, build error, or startup error fails the test.

## License and corresponding source

MinIO remains unmodified and licensed under GNU AGPL v3. The final local image
retains its original `LICENSE`, `NOTICE`, and `CREDITS`, plus the Go license, in
`/usr/share/minio`. `corresponding-source.tar.gz` there contains the complete
MinIO source tree, vendored dependencies with their original license/copyright
files, and this build recipe. It can be extracted using `docker cp` while the
container exists. The project does not bundle the executable in any NuGet
package or publish/redistribute the image. Any future distribution or deployment
needs separate licensing review; keeping this fixture is not a license grant
for other uses. See [upstream compliance guidance](https://github.com/minio/minio/blob/9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a/COMPLIANCE.md).

Updating the fixture requires rechecking upstream advisories, resolving an
immutable official commit and toolchain digest, independently checking the
source SHA-256, and running the unchanged S3 roundtrip on Docker-enabled CI.

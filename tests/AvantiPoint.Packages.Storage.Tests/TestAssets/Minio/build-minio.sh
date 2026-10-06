#!/bin/sh
set -eu

# RELEASE.2025-10-15T17-29-55Z, the final official OSS release; fixes CVE-2025-62506.
readonly MINIO_COMMIT=9e49d5e7a648f00e26f2246f4dc28e6b07f8c84a
readonly MINIO_SOURCE_SHA256=45521908307306e925c98d629e1c17d78c8b72b6ee242b1bfb1409f7d8ee5841
[ "$(go env GOVERSION)" = go1.24.13 ]
wget -T 60 -O /tmp/minio-source.tar.gz \
    "https://codeload.github.com/minio/minio/tar.gz/${MINIO_COMMIT}"
printf '%s  %s\n' "$MINIO_SOURCE_SHA256" /tmp/minio-source.tar.gz | sha256sum -c -
tar -xzf /tmp/minio-source.tar.gz --strip-components=1 -C /src
rm /tmp/minio-source.tar.gz
cd /src

# go.sum plus the Go checksum database authenticate dependencies. No toolchain
# auto-upgrade, direct VCS fallback, package install, or floating module versions.
sha256sum go.mod go.sum > /tmp/go-inputs.sha256
go mod download
go mod verify
go mod vendor
sha256sum -c /tmp/go-inputs.sha256
mkdir -p /out
go build -mod=vendor -p 2 -trimpath -buildvcs=false -ldflags='-s -w' -o /out/minio .
/out/minio --version

# Keep complete corresponding source, vendored dependencies (including their
# notices), and this exact build recipe with the ephemeral executable.
mkdir -p /src/test-fixture-build /out/usr/share/minio /out/data /out/tmp
cp /fixture/* /src/test-fixture-build/
cp LICENSE NOTICE CREDITS /out/usr/share/minio/
cp /usr/local/go/LICENSE /out/usr/share/minio/GO-LICENSE
# Stable ordering/timestamps of the archive are not required for source access;
# the executable is built from the independently pinned inputs above.
tar -czf /out/usr/share/minio/corresponding-source.tar.gz -C /src .
chmod 1777 /out/tmp
chown 10001:10001 /out/data
chmod 700 /out/data
# Build caches never enter the final image and do not survive the build stage.
go clean -cache -modcache

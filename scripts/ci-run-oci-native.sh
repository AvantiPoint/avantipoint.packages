#!/usr/bin/env bash
# Mirrors GitHub Actions ubuntu-latest for native Docker OCI tests.
# Run from repo root on WSL or Linux (NOT inside a Docker container — the daemon must share loopback with Kestrel).
set -euo pipefail

if [[ -f /proc/1/cgroup ]] && grep -q docker /proc/1/cgroup 2>/dev/null; then
  echo "ERROR: Run this script on the host (WSL/Linux), not inside a Docker container." >&2
  echo "Docker push/pull to 127.0.0.1 requires the daemon and test host on the same machine." >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1 || ! docker version >/dev/null 2>&1; then
  echo "ERROR: Docker CLI/daemon is not available." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
  export PATH="$HOME/.dotnet:$PATH"
fi

dotnet restore APPackages.slnx
dotnet build tests/AvantiPoint.Packages.Registry.Oci.Tests/AvantiPoint.Packages.Registry.Oci.Tests.csproj -c Release
dotnet test --project tests/AvantiPoint.Packages.Registry.Oci.Tests/AvantiPoint.Packages.Registry.Oci.Tests.csproj -c Release --no-build

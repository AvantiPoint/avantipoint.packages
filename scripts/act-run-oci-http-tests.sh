#!/usr/bin/env bash
# HTTP-level OCI tests runnable inside nektos/act containers (no native docker CLI networking).
set -euo pipefail

curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
export PATH="$HOME/.dotnet:$PATH"

dotnet restore APPackages.slnx
dotnet build tests/AvantiPoint.Packages.Registry.Oci.Tests/AvantiPoint.Packages.Registry.Oci.Tests.csproj -c Release

dotnet test --project tests/AvantiPoint.Packages.Registry.Oci.Tests/AvantiPoint.Packages.Registry.Oci.Tests.csproj -c Release --no-build -- \
  --filter-class "*OciRegistryTests*" \
  --filter-class "*OciTokenAuthTests*"

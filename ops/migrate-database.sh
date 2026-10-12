#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

: "${ConnectionStrings__SchoolManagement:?Set ConnectionStrings__SchoolManagement from your deployment secret store}"

dotnet tool restore
dotnet ef database update \
  --project src/SchoolManagement.Infrastructure \
  --startup-project src/SchoolManagement.Web \
  --configuration Release

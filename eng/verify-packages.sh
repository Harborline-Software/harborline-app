#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "$0")/.." && pwd)
bash "$repo_root/eng/verify-boundaries.sh"
version=${HARBORLINE_PACKAGE_VERSION:-"0.1.0-preview.local.$(date -u +%Y%m%d%H%M%S)"}
if [ -n "${HARBORLINE_PACKAGE_OUTPUT:-}" ]; then
  artifact_dir=$HARBORLINE_PACKAGE_OUTPUT
else
  artifact_dir=$(mktemp -d "${TMPDIR:-/tmp}/harborline-app-packages.XXXXXX")
  trap 'rm -rf "$artifact_dir"' EXIT
fi

mkdir -p "$artifact_dir"
package_projects=(
  "$repo_root/src/Harborline.App.Abstractions/Harborline.App.Abstractions.csproj"
  "$repo_root/src/Harborline.App.Blazor/Harborline.App.Blazor.csproj"
  "$repo_root/src/Harborline.App.Blazor.Hybrid/Harborline.App.Blazor.Hybrid.csproj"
  "$repo_root/src/Harborline.App.Testing/Harborline.App.Testing.csproj"
)
for project in "${package_projects[@]}"; do
  dotnet pack "$project" -c Release -p:Version="$version" -o "$artifact_dir"
done
python3 "$repo_root/eng/package-proof.py" consume "$artifact_dir" "$version"
python3 "$repo_root/eng/package-proof.py" seal "$artifact_dir" "$version"

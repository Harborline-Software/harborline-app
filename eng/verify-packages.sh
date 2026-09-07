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
# The nuget.org URL is listed FIRST deliberately. With the local feed first, NuGet normalises the
# URL that follows as though it were a path -- the "//" collapses and it is then resolved relative
# to the project directory, so restore dies with
#   NU1301: The local source '...\tests\package-consumer\https:\api.nuget.org\...'
# Observed on SDK 11.0.100-preview.7 under both bash and PowerShell, so it is a NuGet
# argument-handling quirk rather than MSYS path conversion. It bites on every run: each run mints a
# brand-new version, so NuGet can never satisfy the restore from cache and always walks the remote
# source. Listing the URL first avoids it, and is inert wherever the quirk does not reproduce.
#
# harborline-api hit this first and carries the same ordering and the same note in its own
# eng/verify-packages.sh. This copy went unfixed because the job that runs it here has been red
# since 2026-08-22 for an unrelated reason (the gitignored local feed is absent on a clean runner),
# and then stopped running at all when Actions ran out of minutes -- so nothing ever reported it.
dotnet restore "$repo_root/tests/package-consumer/Consumer.csproj" \
  -p:HarborlinePackageVersion="$version" \
  --source https://api.nuget.org/v3/index.json \
  --source "$artifact_dir" \
  --force-evaluate
dotnet run --project "$repo_root/tests/package-consumer/Consumer.csproj" \
  -c Release --no-restore -p:HarborlinePackageVersion="$version"

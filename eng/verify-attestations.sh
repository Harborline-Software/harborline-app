#!/usr/bin/env bash
# Separate signing job (owner ruling 2026-11-03: "Separate signing job"; the same design as harborline-api). publish
# runs this before it pushes anything: every .nupkg and package-manifest.json in <dir> must carry a GitHub artifact
# attestation that this repository's packages.yml signed, for exactly this commit, on a GitHub-hosted runner.
# gh checks the file's own SHA-256 against the attestation's subject, so changed bytes find no attestation.
# consumer-proof.json is bound through the manifest's consumerProofSha256, which package-proof.py verify checks.
# Any failure stops publication: nothing is skipped, and a directory with nothing to verify is refused.
#
# Usage: verify-attestations.sh <dir>. Needs GITHUB_REPOSITORY, GITHUB_SHA and a GH_TOKEN that can read attestations.
set -euo pipefail

dir=${1:?usage: verify-attestations.sh <dir>}
repo=${GITHUB_REPOSITORY:?}
sha=${GITHUB_SHA:?}
[[ $sha =~ ^[0-9a-f]{40}$ ]] || { echo "::error::GITHUB_SHA is not a full commit id: $sha" >&2; exit 1; }

shopt -s nullglob
nupkgs=("$dir"/*.nupkg)
shopt -u nullglob
(( ${#nupkgs[@]} > 0 )) || { echo "::error::no .nupkg in $dir to verify" >&2; exit 1; }
[ -f "$dir/package-manifest.json" ] || { echo "::error::no package-manifest.json in $dir to verify" >&2; exit 1; }

for file in "${nupkgs[@]}" "$dir/package-manifest.json"; do
  if ! gh attestation verify "$file" \
      --repo "$repo" \
      --signer-workflow "$repo/.github/workflows/packages.yml" \
      --source-digest "$sha" \
      --deny-self-hosted-runners >/dev/null; then
    echo "::error::$(basename "$file") has no attestation from $repo's packages.yml for $sha; refusing to publish" >&2
    exit 1
  fi
  echo "verified $(basename "$file")"
done

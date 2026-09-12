#!/usr/bin/env bash
# Local verification for harborline-app — the replacement for GitHub Actions.
#
# Actions is switched off here (see the ACTIONS_ENABLED block at the top of
# .github/workflows/packages.yml): the org is on the free plan, private-repo minutes ran out on
# 2026-08-24, and until these repositories are public the workflows only produce red checks that
# never ran. This runs what those workflows ran, in the same order, and on success records a receipt
# as per-run evidence. Nothing requires that receipt to push: the `verify` check on the self-hosted
# runner is the required gate (app #9), and the pre-push refusal it fed was removed with the rest of
# the retired landing mechanism (ticket 393 item 5).
#
# The step ids MUST stay in sync with requiredStepIds in eng/verify-receipt.mjs.
#
# Not covered, deliberately: the `publish` half of pack-consume-publish. Publishing is a release
# action, not a verification, and it stays gated on a tag or an explicit dispatch.
set -uo pipefail

root=$(git rev-parse --show-toplevel)
cd "$root"

passed=()
step() {
  local id=$1; shift
  printf '\n\033[1m── %s\033[0m\n' "$id"
  if "$@"; then
    passed+=("$id")
  else
    printf '\n\033[31mFAILED: %s\033[0m\n' "$id" >&2
    printf '  command: %s\n' "$*" >&2
    printf '\n  No receipt written. Fix this and re-run: bash eng/verify.sh\n' >&2
    exit 1
  fi
}

for tool in dotnet node; do
  command -v "$tool" >/dev/null || { echo "required tool not on PATH: $tool" >&2; exit 1; }
done

# The local package feeds are gitignored, and both lanes consume the platform as a PACKAGE rather
# than a project reference. CI rebuilds them from a pinned platform checkout; here they are expected
# to exist already. Without this check the failure surfaces as NU1301 deep inside a restore, which
# reads like a broken commit rather than a missing prerequisite.
for feed in apps/react/.feed apps/blazor/.feed; do
  [ -d "$feed" ] || {
    echo "missing local package feed: $feed" >&2
    echo "  build it first:  (cd apps/react && HARBORLINE_PLATFORM_REPO=<platform-checkout> npm run feed)" >&2
    exit 1
  }
done
started=$SECONDS

# First, and cheap: this repository is meant to carry no trace of the consumer codename, in its
# code, paths, fixtures, or checks.
step boundaries        bash eng/verify-boundaries.sh

# The React lane is the parity AUTHORITY for every pillar admin surface. Install from the lockfile
# first: checking only that node_modules existed let stale first-party packages typecheck instead of
# the contracts and UI tarballs the app actually declares in package-lock.json.
step react-typecheck   bash -c 'cd apps/react && npm ci && npm run typecheck'
step react-test        bash -c 'cd apps/react && npm test'
step react-build       bash -c 'cd apps/react && npm run build'

if [ "${HARBORLINE_GATE_COVERAGE:-}" = "1" ]; then
  rm -rf artifacts/quality/coverage/dotnet
  step dotnet-test       dotnet test Harborline.App.slnx -c Release --settings eng/coverage.runsettings --collect:"XPlat Code Coverage" --results-directory artifacts/quality/coverage/dotnet/results
  step dotnet-coverage   node eng/coverage.mjs artifacts/quality/coverage/dotnet/results dotnet
else
  step dotnet-test       dotnet test Harborline.App.slnx -c Release
fi
step packages          bash eng/verify-packages.sh
step host-manifest     bash -c 'cd hosts/react-native && npm run validate'

printf '\n\033[32mAll %d steps passed in %dm%02ds\033[0m\n' "${#passed[@]}" "$(((SECONDS-started)/60))" "$(((SECONDS-started)%60))"
if [ "${HARBORLINE_GATE_COVERAGE:-}" = "1" ]; then
  node eng/verify-receipt.mjs --record "${passed[@]}" --coverage-summary artifacts/quality/coverage/dotnet/coverage-summary.json
else
  node eng/verify-receipt.mjs --record "${passed[@]}"
fi

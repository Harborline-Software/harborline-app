set -euo pipefail
(cd apps/blazor && node scripts/build-local-feed.mjs)
dotnet tool restore
node eng/stryker.mjs run --all

# T-428 — compiled catalogue inspector retirement

Implemented against App baseline `d2f7d53` (phase-one controls, PR #32).
The immutable retirement inventory remains pinned to
`11e38b663bb7e953bae9596af12bf9788b1edc7e`.

## Removed production surface

All 90 paths in [the retirement manifest](../../tests/fixtures/compiled-inspector-controls/retirement.manifest.json)
are absent: 60 React files and 30 Blazor files across these ten roots.

- `apps/react/src/admin/forms`
- `apps/react/src/admin/reports`
- `apps/react/src/admin/views`
- `apps/react/src/admin/data-exchange`
- `apps/react/src/admin/scheduling`
- `apps/blazor/Admin/Forms`
- `apps/blazor/Admin/Reports`
- `apps/blazor/Admin/Views`
- `apps/blazor/Admin/DataExchange`
- `apps/blazor/Admin/Scheduling`

The five legacy route branches, navigation entries, providers and clients have been removed from
React App, Blazor Shell and Blazor Program. Blazor's five inspector configuration sections and
Development fixture opt-in have retired. Configure `Workshop__BaseUrl` explicitly.
React retains `VITE_FORMS_API_ORIGIN` as its existing generic node transport setting.

The five canonical controls remain at `tests/fixtures/compiled-inspector-controls/{forms,reports,views,data-exchange,scheduling}.json`.
Each still compares definition and version rows with both original lane fixtures read from the immutable
Git baseline. No production code imports the controls. The 15 Blazor test files which instantiated
the deleted clients/pages also retire; shared-runtime and canonical-control tests replace their
catalogue retirement coverage.

## Verification on 2026-09-15

The package pin remains released Platform `5656aa518fea4a19ef692c6687a38d981fe727fb`.
Local feeds were copied from the existing `polish-dark-repin` checkout at that same recorded pin.
React lockfile integrity was checked by `npm ci --ignore-scripts`; no package dependency or lockfile
changed.

- `npm run typecheck`, `npm test`, `npm run build` in `apps/react`: pass;
  102 tests passed, two existing live-only tests skipped.
- `dotnet test Harborline.App.slnx -c Release`: pass;
  30 architecture tests and 112 Blazor tests passed, two existing live-only tests skipped.
- `bash eng/verify-packages.sh`: boundary checks, four package builds and package consumer pass.
- `npm --prefix hosts/react-native run validate`: pass.
- `git diff --check`: pass.

Both lanes exercise 20 missing/refused-seed combinations: five catalogue families,
Health/Browse, HTTP 403/404. Each proves no grid, actions or alternate view lookup.
Five deep-link cases per lane prove an unconfigured navigation seed cannot restore a retired route.
Unknown definition kind and unknown view kind remain inert. Existing first-run and Authorization
tests still pass.

The architecture fences discover retired public/exported symbols from the immutable source
inventory, scan production for those symbols/routes and control dependencies, and require every
retained administration file and entry point to exist in the baseline. They also reject newly
declared administration symbols and Razor page routes in retained files.

## Executed mutation evidence

Each mutation below was physically added to the worktree, tested with the compiled architecture
test assembly, and then deleted. Each command exited 1 with the named assertion failing.

```text
dotnet test tests/Harborline.App.Tests/Harborline.App.Tests.csproj -c Release --no-build --filter FullyQualifiedName~<test> --nologo -v quiet
```

- `apps/react/src/admin/forms/RetirementCanary.tsx`: `All_ten_retired_roots_are_absolutely_empty` failed (1 failed, 0 passed).
- `apps/react/src/retirement-canary.ts`: `Production_has_no_retired_symbols_routes_or_test_control_dependencies` failed (1 failed, 0 passed).
- `apps/react/src/admin/RetirementCanary.tsx`: `Baseline_diff_adds_no_compiled_administration_file_or_entry_point` failed (1 failed, 0 passed).

The first mutation restored a compiled inspector root, the second imported a canonical test control
from production, and the third added a new compiled administration page. All mutation files were
removed before the final green tests.

## Release condition

Local removal and mutation proofs are complete. This change consumes no unpublished API or
Platform contract. The App PR can run its own CI independently of API host-count baselines.

M6 still needs live acceptance against released API T-427/T-426 surfaces: 13 authentic sources,
their Health/Browse grids, and the typed read-only Form detail in both lanes. These local tests
do not claim that cross-repository acceptance. T-176 retains its first-install and other
non-catalogue obligations.

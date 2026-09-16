# M6 App integration checkpoint

This is provisional integration evidence, not a released Platform pin or M6 acceptance.

Merge `cc17ef5` combines T427 `6256b73` with T433
`e34bf3263d81c7beac9df18099ceeac13dfd0c78`. The isolated branch is
`feat/m6-app-integration`; neither source worktree was changed.

## Reconciliation decisions

- Keep T427's selected-session transport, binary responses, cancellation permits,
  timeout handling, audience split, independent cancellable navigation reads, and
  runtime epoch/data-source guards.
- Add T433's generic React/Blazor PackActionHost routes and shared static assets.
  Keep T427's CatalogueDetail inspector. Legacy AccessHolders survives only as a
  test fixture, never as a production route or bundle import.
- Keep both dev/preview proxy tests and restore T433's production-bundle absence
  assertions. Keep the T427 feed-builder pnpm-lock selection.
- Preserve the Platform pin at `5656aa518fea4a19ef692c6687a38d981fe727fb`.

## Disposal regression

Unmount previously released the host reference without revoking a queued pack
mutation. The shared runtime now exposes idempotent, terminal `dispose()`: abort
its current scope, invalidate pending publications, clear actionable state, and
refuse reuse. Hosts call it before releasing references. React creates a fresh
runtime for each effect lifetime, including StrictMode replay; Blazor cleans up
late import/runtime creation and suppresses post-disposal navigation callbacks.
An already-dispatched mutation is never retried or claimed to be rolled back.

Red evidence reproduced two missing shared-runtime disposal cases, one real
React queued-action dispatch after unmount, and one Blazor missing revoke before
reference release. The first React test draft timed out because it clicked before
the action form was ready; the corrected red-v2 waits for both action controls.
The first expanded Blazor run failed its interop test-double analyzer guard; that
compile log is retained alongside the corrected green run.

## Verification (2026-09-16)

The ignored feeds were copied from the T427 worktree for local integration tests.
Blazor cohort: `0.0.0-alpha.0.h7cfa409885af`. These provisional packages are not a
claim that the unchanged pin supplies the new readonly SchemaForm contract.

- Shared Node suite: 61 passed, no skips or failures.
- React Vitest: 136 passed, one existing live-test skip, 18 files passed.
- React typecheck and production build: passed.
- Real Vite dev/preview proxy and production-bundle checks: 2 passed.
- Blazor Release suite: 199 passed, one existing live-test skip, 200 total.
- Blazor Release publish and git diff whitespace check: passed.

Local evidence lives under `artifacts/m6-integration/`:

| Artifact | SHA-256 |
| --- | --- |
| `final-react.log` | `3d8dd9e45f9cc9f4fd879464042b24f9774f9074c0e034d6958abdf757c821be` |
| `blazor-final/final-v2.trx` | `b86c64d99219a7a022c4a6d3097e335f2cf063cfa331db1c4dabfdee3bc7aca3` |
| `preview-final.log` | `94c806f500cc49bd713cbbb198c7e8456ffd64f84144f53038d2ccdb194c0eae` |
| `dispose-shared-red.log` | `abb997a60f299403feaadfe8d80914e8b1db72147a89efda3f9b0f00b2537234` |
| `dispose-react-red-v2.log` | `3f3a275f651b94f85ba675cbcafe2d8208703667d70c02e5f633ea82cc80d56b` |
| `blazor-dispose-red/red.trx` | `51d50f80257467aef76562b562c6f2a32ad6e113169496268e0c91b714394f3c` |

Remaining release work: independent review; pin the released T427 Platform SHA;
rebuild both feeds from that exact source; run the full App gate and the clean-node
two-person/browser acceptance. No M8/M9 work or release claim is included here.

## Mounted host review repairs (2026-09-16)

Three independently reproduced defects were repaired on top of `9617a9f`:

- Blazor initialization records an attempted view without treating failed import or
  runtime creation as a usable runtime. Explicit Reload performs fresh initialization;
  it never replays an action automatically.
- Blazor interop failures display fixed safe text rather than exception prose.
  Unowned cancellation/timeout becomes that visible failure; actual caller/lifetime
  cancellation remains cancellation. Linked interop tokens retain the configured
  circuit timeout. Host disposal still revokes the shared JavaScript runtime before
  reference release; canceling a .NET await alone does not claim to undo browser work.
- React keys form/file inputs per action opening. Reopening clears native file state,
  and selecting the same File again enables submission.

Mounted red tests reproduced import/create recovery failures (2), unowned interop
timeouts (4), disabled configured deadline (1), and retained native file selection
(1). The final mounted Blazor host suite passes all 16 cases, including caller and
lifetime cancellation controls. No mutation retry was introduced.

The final rerun uses the same provisional feeds described above:

- Shared Node: 61 passed.
- React: 137 passed, one existing live skip, 18 files passed.
- Blazor Release: 210 passed, one existing live skip, 211 total.
- React typecheck/build, dev/preview proxy and bundle checks (2), Blazor Release
  publish, and whitespace check all passed.

Preserved evidence under `artifacts/m6-integration/`:

| Artifact | SHA-256 |
| --- | --- |
| `p2-full-react-v2.log` | `ca1c09ae854623a0833f20fac009caf8005794db5ff137270d35024de14778a5` |
| `p2-blazor/full-v2.trx` | `f43b83c34d44e2d6e3b9295562dfa4f629e0aabd4793a2903eb56625cef5a962` |
| `p2-preview-v2.log` | `7bb9fead9338fc3f8788320b476b16bdb4bf3981e0c734a08eeef387c849bdda` |
| `p2-file-red.log` | `82bbd148bf7faa05a7442b9a94899731d87ebc8ecb8b05931cf9239b3bab0a60` |
| `p2-blazor/init-red.trx` | `b86ce27cbab19f3e34eb2b6117f819fffa8d110066933cc666352f92746d8ad5` |
| `p2-blazor/timeout-red.trx` | `8fd79ecc1269bccaaea012f2af639e08db6f6f7c9e6a6f753597b2c32fb28eb9` |
| `p2-blazor/deadline-red.trx` | `7239a05b8fd323ded95ea2e153c29a54b9c937cd0b74d26a1d3874a56e9bfd12` |
| `p2-blazor/deadline-green.trx` | `af4fb8d2f58d097719b49f1a9b07339df193967278377fdfbbbca2621a89f0cd` |

The Platform pin and release/acceptance boundary remain unchanged.

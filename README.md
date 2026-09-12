# Harborline App

This repository begins with a fresh public history as of September 2026. The earlier private history is kept, unchanged, in the private archive repository, and every design decision it records is carried forward in the Harborline control tickets. Nothing was rewritten; the history simply starts here.


> **Status: pre-release.** Harborline is under active development and is not ready for production use. APIs, schemas, storage formats and package names change without notice, and there are no supported installs yet. Source is licensed under [Apache-2.0](LICENSE); see [NOTICE](NOTICE) and the [trademark policy](TRADEMARKS.md).

Harborline App is the reference app and an extension host for independently versioned product consumers. Its product interfaces are shared across React and Blazor; framework UI and native-device implementations stay inside their projections.

Current executable preview packages:

- `Harborline.App.Abstractions` — capability, navigation, user-session, and extension composition.
- `Harborline.App.Blazor` — the provenance-preserving Blazor application shell.
- `Harborline.App.Blazor.Hybrid` — native host seams used by .NET MAUI desktop/iOS/Android adapters.
- `Harborline.App.Testing` — explicit test sessions, extensions, secure storage, and media adapters.

Host envelopes are registered in `hosts/host-manifest.json`. React Native iOS/Android and .NET MAUI Blazor Hybrid iOS/Android are required Harborline projections. External consumers select only the projection packages required by their own host strategy.

## Which hosts you can actually run

Read this before trying to run a mobile or desktop host. Most entries in `hosts/host-manifest.json` are **registered envelopes, not runnable applications**, and that is deliberate.

| Host             | Targets                             | State                                  | Runnable today                                                 |
| ---------------- | ----------------------------------- | -------------------------------------- | -------------------------------------------------------------- |
| `blazor-web`   | browser                             | `reference-envelope-created`         | **Yes** — `apps/blazor` |
| `blazor-maui`  | windows, mac-catalyst, ios, android | `envelope-created-toolchain-pending` | No                                                             |
| `react-native` | ios, android                        | `envelope-created`                   | No                                                             |
| `react-web`    | browser                             | `source-migration-pending`           | **Yes** — `apps/react` (see below)                             |
| `react-tauri`  | windows, macos, linux               | `source-migration-pending`           | No                                                             |

The MAUI executable project is **intentionally not generated** until the workload, bundle identifiers, signing teams, and minimum OS versions are recorded — see `hosts/blazor-maui/README.md`. An empty project would falsely claim a green mobile projection. The React Native host is `generation-pending-interface-freeze` and carries only a validation script.

So: there is exactly one runnable application in this repository, and it runs the same way on all three operating systems.

## Prerequisites

**The .NET SDK version is pinned exactly.** `global.json` sets `11.0.100-preview.7.26381.103` with `"rollForward": "disable"`, so a newer or older SDK will not be substituted — `dotnet` fails instead. Check what you have:

```sh
dotnet --list-sdks
```

If that exact version is absent, install it. Distribution package feeds rarely carry preview SDKs, so the install scripts are the reliable route on every platform:

| Platform          | Install                                                                                                                                                                                                         |
| ----------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **macOS**   | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --version 11.0.100-preview.7.26381.103` — match your architecture; Apple Silicon needs the arm64 build, which the script selects automatically. |
| **Linux**   | `curl -sSL https://dot.net/v1/dotnet-install.sh \| bash -s -- --version 11.0.100-preview.7.26381.103`                                                                                                          |
| **Windows** | `powershell -c "& ([scriptblock]::Create((irm https://dot.net/v1/dotnet-install.ps1))) -Version 11.0.100-preview.7.26381.103"`                                                                                |

The install scripts place the SDK under `~/.dotnet` (`%USERPROFILE%\.dotnet` on Windows) rather than system-wide; add that directory to `PATH` if `dotnet --list-sdks` does not show the new version.

**Bash** is required for `eng/verify-boundaries.sh` and `eng/verify-packages.sh`. macOS and Linux have it. On Windows use **Git Bash** (shipped with Git for Windows) or WSL — both work; PowerShell and `cmd` do not run these scripts.

**Node.js 22 or newer** is required only to validate the React Native host envelope (`hosts/react-native/package.json` declares `"node": ">=22"`).

## Run the app

```sh
dotnet run --project apps/blazor
```

Identical on macOS, Linux and Windows. It is a Blazor Server application using interactive server rendering, so it serves over HTTP and needs no client-side build step.

The host now carries `Properties/launchSettings.json`, which pins **`http://localhost:5321`** and sets
`ASPNETCORE_ENVIRONMENT=Development`. Both matter:

- **The port** avoids the default 5000, which collides often — on macOS because AirPlay Receiver
  listens there. Override with `--urls "http://localhost:PORT"` if 5321 is taken.
- **The environment is load-bearing, not a preference.** Static web assets — the framework
  `_framework/blazor.web.js`, the shell's scoped CSS under `_content/`, and the host's own
  `.styles.css` — are only mapped from the static-web-assets manifest in `Development`. Run this
  host in `Production` from a build output and every one of those returns **500**, leaving an
  unstyled page with no interactivity. `dotnet publish` materialises a real `wwwroot` and does not
  have this problem; running from `bin/` does.

Then open the URL the process prints — it logs `Now listening on: <url>` at startup. `/` serves the
application shell. Stop it with `Ctrl+C`.

If `/` returns **404** while static files still return 200, the routable page is missing: the host
(or some component in the assembly) must carry a `@page` directive, or `MapRazorComponents<App>()`
discovers no route and matches nothing.

To run in another environment, override it explicitly — and expect the asset behaviour above:

```sh
ASPNETCORE_ENVIRONMENT=Production dotnet run --project apps/blazor   # macOS, Linux
```

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"; dotnet run --project apps/blazor   # Windows PowerShell
```

Outside `Development` the Forms admin client must also be configured, or startup fails loudly
with a configuration error (ticket 153 — the fixture is an explicit opt-in, never a silent
fallback). Either set `FormsAdmin:BaseUrl` (env: `FormsAdmin__BaseUrl`) to the local node
origin, or opt in to the serverless fixture with the boolean `FormsAdmin:UseFixture=true`
(env: `FormsAdmin__UseFixture=true`); `appsettings.Development.json` sets the opt-in for
Development. The React lane's equivalents are `VITE_FORMS_API_ORIGIN` and
`VITE_FORMS_FIXTURE`, which accepts exactly `1` or `true`.

The reference host does not configure an HTTPS endpoint, so `dotnet dev-certs https --trust` is not needed. If you add one, note that `--trust` is supported on Windows and macOS but not on most Linux distributions, where the certificate must be trusted manually.

## Verify

`eng/verify.sh` is this repository's gate. It runs as the required `verify` check on a self-hosted
macOS runner, on pull requests and on merge-queue groups (`.github/workflows/verify.yml`), and it is
the same script you run locally:

```sh
bash eng/verify.sh                    # run on a clean tree; the receipt attests to HEAD
```

On success it records a receipt as per-run evidence. Nothing requires that receipt to push: the
required check is what decides whether a change lands, and the `.githooks/pre-push` refusal that
used to demand a local receipt was removed with the rest of the retired landing mechanism (control
ticket 393 item 5). Landing is `gh pr merge --auto`; the merge queue builds and judges the tree it
lands.

The receipt is refused if the working tree is dirty: it attests to HEAD, while `eng/verify.sh` runs
against the working tree, so on a dirty tree it would vouch for code the run never saw. Commit
first, then verify.

Individual steps, to run one on its own:

```sh
dotnet test Harborline.App.slnx
bash eng/verify-boundaries.sh
bash eng/verify-packages.sh
(cd apps/react && npm test)
(cd hosts/react-native && npm run validate)
```
## Publishing

Package publishing is prerelease-only and fails closed until `repository.yaml` names a distribution owner and marks the approved remote `active`. The workflow uses GitHub's repository-scoped `GITHUB_TOKEN`; do not add a long-lived package token unless the approved organization policy requires one.

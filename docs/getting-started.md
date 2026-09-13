# First use of the M4 Workshop candidate

> **Acceptance pending:** This is the readiness page and source-reviewed onboarding preview for the selected **M4 Workshop candidate**. Immutable repository pins and a recorded person-driven run through React and Blazor are still required before it becomes release-supported onboarding.

The candidate exercise is designed to show one small, safe path: establish the first administrator on a new local node, open Workshop, validate and transport a synthetic Notes package, create one Note record, read it back and inspect an authorization or validation explanation.

Use only a disposable node and synthetic values while the candidate is under test. Do not point this guide at a customer environment or a directory containing records you need to keep.

## What is available now

| Part of the lesson | Current evidence | Status in this guide |
|---|---|---|
| App and node prerequisites | SDK, package and source-build manifests exist. | Source-reviewed commands below. |
| Installation account and first administrator | A local credential ceremony creates the installation account and root installation grant; a separate installer path establishes or projects the signed live-roster party as administrator. | Boundaries documented; the complete release invocation still needs acceptance evidence. |
| Seeded Workshop lists | React and Blazor source render package-provided list definitions through their projection lanes. | Candidate behavior; full lane run pending. |
| Validate, export, verify, install and activate a small pack | The node routes, operator commands and synthetic Notes fixture exist; the interactive workflow is still changing in the active M4 task. | Sequence documented; do not treat it as a passed exercise. |
| Create and read a form-bound record | Candidate implementation and review are still in progress. | Expected flow only; exact UI and response examples await the accepted candidate. |
| Authorization and validation explanation | The candidate intends to return an audit identifier for an accepted write and field pointers for validation refusals. | Expected flow only; example output awaits the accepted candidate. |

## Prerequisites

Current candidate evaluation uses sibling source checkouts of [App](https://github.com/Harborline-Software/harborline-app), [API](https://github.com/Harborline-Software/harborline-api) and [Platform](https://github.com/Harborline-Software/harborline-platform). Use the immutable commits that the M4 release notes name when those pins are recorded; moving development heads are not candidate pins.

The React application declares Node.js 22 or newer in [its package manifest](../apps/react/package.json). The Blazor App and API use the exact preview .NET SDK selected by each repository's `global.json`; roll-forward is disabled.

From each checkout, verify the installed tools before preparing local feeds:

```powershell
node --version
npm --version
dotnet --version
dotnet --list-sdks
```

Stop if `dotnet --version` differs from the SDK selected by that checkout or Node does not satisfy the React manifest. Installing a nearby preview is not equivalent when roll-forward is disabled.

## Prepare a source-development host

The commands in this section describe source composition and have not been promoted to a release install procedure. They were checked against the current project paths and scripts, but the complete sequence still requires the M4 acceptance run.

Build the React lane's local Platform feed from the App checkout, then restore its locked dependencies:

```powershell
Set-Location apps/react
npm run feed
npm ci
```

The feed script uses the sibling `harborline-platform` checkout by default. Set `HARBORLINE_PLATFORM_REPO` to an absolute path only when your Platform checkout is elsewhere, and make that checkout match the release notes' future pin.

Build the Blazor lane's local Platform feed from the App checkout:

```powershell
Set-Location apps/blazor
node scripts/build-local-feed.mjs
```

Both feed scripts pack Platform outputs before the App consumes them. A source or workspace reference can hide missing package contents and does not represent the candidate boundary.

## Prepare the installation account and first administrator

The candidate has two distinct startup boundaries. The installation credential ceremony creates the installation account, root binding and root installation grant without an anonymous HTTP claim route; the administrator path separately resolves the node's party from the signed live roster and establishes that party once through installer authority, or projects the already durable administrator on a later boot.

Generate an Argon2id password hash for the installation account before starting the node and put only the hash in the node environment.

From the API checkout, run the password tool with no password argument and enter a synthetic password on standard input so the plaintext does not enter shell history:

```powershell
dotnet run --project apps/local-node-host/Harborline.LocalNodeHost.csproj -- hash-web-password
```

Capture the printed PHC hash in a secret-capable local mechanism, then supply the first user's name through `LocalNode__WebClient__FounderUsername` and the hash through `LocalNode__WebClient__FounderPasswordHash`. Do not commit either value, and never put the plaintext password in configuration.

These username and password-hash settings do not select the administrator party or confer tenant administration by themselves. The administrator establishment reads the signed live roster, binds the local node through its signing key and calls the one-shot installer authority only when that team has no durable administrator; later boots project the durable record without recreating it.

Create a fresh, high-entropy `LocalNode__SessionToken` for each boot. The node and either development App lane must receive the same token; it is a bearer credential for loopback requests and must not be saved in a repository, copied into a browser bundle or posted in an issue.

The acceptance run must record how the candidate creates the signed live-roster member, the resulting installer-establishment disposition, the remaining first-boot settings, the disposable data-directory location and the exact node launch command. Do not infer a complete setup by joining the two source-level ceremonies yourself.

## Choose a session type

### Connected local runtime

The M4 exercise requires a connected local node because package transport, durable record creation, validation and authorization traces are runtime behavior. Configure the App lane with the node's loopback origin and the same per-boot session token.

For React development, the maintained configuration names are `VITE_FORMS_API_ORIGIN`, `VITE_AUTHORIZATION_API_ORIGIN` and `LOCAL_NODE_SESSION_TOKEN`. The Vite development proxy attaches the token; a production bundle has no such proxy, so this setup proves only the development-host path.

For Blazor development, the maintained configuration names include `FormsAdmin__BaseUrl`, `AuthorizationAdmin__BaseUrl` and `LocalNode__SessionToken`. The server-side Blazor host attaches the token without sending it to browser code; the accepted M4 source may add a Workshop-specific base URL, which the release notes and App settings must identify.

Start a lane only after the node has reported its actual loopback address and bootstrap result:

```powershell
# React, from apps/react
npm run dev

# Blazor, from the App repository root
dotnet run --project apps/blazor
```

React's development server is declared at `http://localhost:5322`; Blazor's launch profile is declared at `http://localhost:5321`. These are development addresses, not released service endpoints.

### Fixture session

Explicit React and Blazor fixture clients exist for selected administration surfaces. A fixture session is useful for isolated UI development but does not start a node, establish a real administrator, install a signed pack, persist the Note record or return a connected authorization trace.

Do not use fixture output as evidence that the M4 exercise passed. The release-supported first-use lesson must run against the connected runtime in both lanes from the same accepted synthetic fixture.

## The candidate workflow

The accepted flow is expected to expose these actions from definitions supplied by the active Platform package: validate the package document, export its signed artifact, verify it, install it, activate it, create a record and read the record with its explanation. The action labels and order must come from the accepted candidate UI; this guide does not substitute a second command-line acceptance script.

### 1. Open Workshop

Open the App address for the selected lane and choose **Workshop** in the workspace navigation. The candidate Platform package is expected to supply the workspace and its definition lists, including **Record types** and **Forms**.

Confirm that the list content came from the connected node. A fixture banner, configuration error or list request error means the exercise has not started successfully.

React and Blazor are expected to expose the same workspace, rows and workflow actions through their respective projections. Genuine presentation differences may follow browser and framework conventions, but the accepted action sequence and outcomes must remain equivalent.

### 2. Use the synthetic sample

Use only the Notes sample named by the accepted release notes. The current source fixture is designed around package key `harborline.notes-lite`, record type `notes.entry`, form `notes.capture` and a required text field; those identifiers remain candidate data until the M4 runbook seals the fixture.

Enter an obviously synthetic value such as `M4 onboarding sample — delete me`. Do not include a person's name, email address, customer identifier, credential, access token or real operational observation.

### 3. Validate before export

Choose the candidate's **Validate** action before exporting. A successful result should identify the package document as valid; an invalid package definition should display a stable reason and JSON Pointer locations where the response supplies them.

The exact package-validation codes and example output must be copied from the accepted run rather than invented here.

### 4. Export, verify, install and activate

After validation, follow the declared actions in order: **Export**, **Verify**, **Install** and **Activate**. Keep the exported `.pack` artifact inside the disposable exercise directory and do not substitute an unrelated or customer-authored package.

Verification must report the node's verdict. Installation and activation are distinct operations, and activation is successful only when the candidate reports activation without projection or Platform refusals.

The API's maintained [operator CLI guide](https://github.com/Harborline-Software/harborline-api/blob/main/apps/node-operator-cli/README.md) documents the corresponding headless verbs for operators. Those verbs are useful for diagnosis but do not replace the person-driven App acceptance required for this lesson.

### 5. Create the Note record

After activation, choose the action that creates a record from the active package's bound form. Enter the synthetic sample value and submit it once.

The accepted candidate must prove that the runtime authorizes the write before validation and persistence, validates against the bound form, stores the canonical record and returns the accepted decision's audit identifier. The active implementation is still under review, so this guide does not show fabricated success output.

To find a record-validation explanation, clear the Note form's required text field in the disposable exercise and submit once. Record the refusal reason and JSON Pointer without copying the submitted body, restore the synthetic value, and submit the valid record.

### 6. Read the record and its explanation

Choose the read action for the record you just created. Confirm that the returned record identifier and synthetic value match the write rather than assuming that a success message proves persistence.

Open the authorization explanation addressed by the write's audit identifier. The accepted candidate should show the decision that allowed the write; if the response has no audit identifier or the trace cannot be read in the same session, record the defect against the existing M4 task rather than rewriting the lesson around it.

An authorization explanation answers why the acting principal could perform the operation. A validation explanation answers why submitted values met or failed the form's checks. Neither explanation proves that the real-world claim in a record is true.

## Clean up the disposable exercise

Stop both development hosts, clear the process environment values that held the username, password hash, session token and node origin, and remove only the disposable data directory and exported sample artifact whose paths you recorded before starting.

Do not delete a default or shared node directory based on a guessed path. The accepted runbook must name its temporary directory and cleanup command so the target can be checked before removal.

The candidate does not yet document an in-product record deletion path for this exercise. Treat creation as persistent within the disposable node and discard the entire verified disposable environment after the run.

## Record a documentation result

For each lane, record the release identifier, App/API/Platform pins from release notes, operating system, browser, exact starting page, action labels used and whether the final record and explanation were visible. Do not include the password hash, session token, root seed, personal data or record body.

Report confusing steps, broken public links and sanitized errors in [GitHub Issues](https://github.com/Harborline-Software/harborline-app/issues). Functional M4 defects remain with the implementation task; documentation observations should describe the point of confusion without silently changing the acceptance sequence.

The initial author review found that the App README mixed source-host instructions with the missing user entry path. The [solution overview](solution-overview.md), this guide and the README navigation resolve that issue, while unfamiliar-reader validation remains an external release prerequisite.

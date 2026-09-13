# Prepare the Workshop development environment

> **Candidate preparation:** This source-development setup is incomplete as an end-to-end installation procedure. Check [documentation readiness](release-readiness.md) for the missing startup evidence before attempting the [first-use exercise](getting-started.md).

## Prerequisites

Current candidate evaluation uses sibling source checkouts of [App](https://github.com/Harborline-Software/harborline-app), [API](https://github.com/Harborline-Software/harborline-api) and [Platform](https://github.com/Harborline-Software/harborline-platform). Use the immutable commits that the M4 release notes name when those pins are recorded; moving development heads are not candidate pins.

The React application declares its Node.js requirement in [its package manifest](../apps/react/package.json). The Blazor App and API use the exact preview .NET SDK selected by each repository's `global.json`; roll-forward is disabled.

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

Start in the App repository root for each command block. Build the React lane's local Platform feed, then restore its locked dependencies:

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

Open the development address printed by the selected host. These addresses belong to the development environment.

### Fixture session

Explicit React and Blazor fixture clients exist for selected administration surfaces. A fixture session is useful for isolated UI development but does not start a node, establish a real administrator, install a signed pack, persist the Note record or return a connected authorization trace.

Do not use fixture output as evidence that the M4 exercise passed. The release-supported first-use lesson must run against the connected runtime in both lanes from the same accepted synthetic fixture.

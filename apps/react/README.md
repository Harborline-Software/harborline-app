# Harborline App — React web

The React web application. Its Blazor counterpart is `apps/blazor`; both compose the **same
platform-generated modules**, which is what keeps the two lanes visually and behaviourally
identical.

## Consuming the platform

This app consumes `@harborline-software/ui-react` as a **packed package**, never as a source or
workspace reference:

```sh
npm run feed                                              # packs the platform package into .feed/
npm install ./.feed/harborline-software-ui-react-0.7.0-alpha.tgz
npm run dev
```

`npm run feed` reads the platform repo from `HARBORLINE_PLATFORM_REPO`, defaulting to a sibling
`harborline-platform` checkout. It runs `npm ci` and `npm run build` in the platform projection
before packing, then refuses a tarball that carries no `package/dist/` — because the platform
gitignores `dist/` and the package has no `prepack` script, so packing without building produces
an archive containing no code at all. That failed silently on a clean machine and looked fine on
a developer's, where a stale `dist/` was lying around.

CI runs this same script against a platform checkout pinned in `eng/platform-pin.json`, so the
feed a runner builds and the feed you build come from the same commit by the same path.

**Why packed rather than a path reference.** A source reference resolves `dist/` and `style.css`
from disk, so the app would render correctly while the published package carried neither. That is
not hypothetical — it is exactly the defect the Blazor lane shipped, where the scoped stylesheet
existed in `obj/` and never reached the browser. Packing proves the files listed in the package's
`files` field are the files the app actually receives.

## Run

```sh
npm run dev        # http://localhost:5322
npm run build      # production bundle
npm run preview    # serve the built bundle
npm run typecheck
```

The built-bundle preview host supports the same cookie-only selected-session transport as
development. Configure `VITE_FORMS_API_ORIGIN` with the fixed local-node origin, build, then run:

```sh
pnpm build
node node_modules/vite/bin/vite.js preview --host 127.0.0.1 --port 5322 --strictPort
```

This command owns a single foreground listener and serves `dist/`; the build's
`dist/.vite/manifest.json` names its hashed assets. A supervising process can record the checkout
revision, build/asset hashes, process ID, and listener without a diagnostics endpoint. Browser
requests use same-origin `/api/selected-node/` routes, selected-session cookies, and antiforgery.
Preview explicitly disables the legacy development bearer proxy. A missing selected session
never borrows `LOCAL_NODE_SESSION_TOKEN`. This is a local preview host, not an internet-facing
deployment server. `pnpm test:preview` builds and tests the real preview listener against two
isolated session identities.

## Verified

- `tsc -b --noEmit` passes against the packaged type definitions, not the platform source.
- `npm run build` emits a bundle carrying ~175 KB of platform CSS, which is the design system
  arriving through the package boundary.

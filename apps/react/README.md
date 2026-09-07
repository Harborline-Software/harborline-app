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

## Verified

- `tsc -b --noEmit` passes against the packaged type definitions, not the platform source.
- `npm run build` emits a bundle carrying ~175 KB of platform CSS, which is the design system
  arriving through the package boundary.

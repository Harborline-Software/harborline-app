/// <reference types="vite/client" />

// This file did not previously exist in the app: nothing under src/ touched import.meta.env
// before the Forms admin client selection (src/admin/forms/client/index.ts). Without it,
// `import.meta.env` fails `tsc -b --noEmit` (the app's typecheck script) because the vite/client
// ambient types are never referenced by tsconfig.json.

import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// The React lane consumes @harborline-software/ui-react as a PACKAGE, never as source.
// See README.md: a source or project reference would resolve assets from disk and hide the
// exact class of packaging defect the Blazor lane just shipped (a package that builds locally
// but carries no stylesheet).
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const origin = env.VITE_FORMS_API_ORIGIN ?? env.VITE_REPORTS_API_ORIGIN ?? env.VITE_VIEWS_API_ORIGIN ?? env.VITE_DATA_EXCHANGE_API_ORIGIN ?? env.VITE_SCHEDULING_API_ORIGIN ?? env.VITE_AUTHORIZATION_API_ORIGIN

  // Ticket 093. The local node's listener gate is gate-all-by-default, so an unauthenticated
  // request answers 401 and every admin surface's HTTP client showed an error instead of data.
  // The credential is attached HERE, by the dev server, and deliberately NOT in the browser
  // bundle: loadEnv is called with an empty prefix, so this variable is readable by the config
  // but is never one of the VITE_-prefixed values Vite inlines into client code. A token in a
  // bundle is a token in every browser cache that ever loaded the app.
  //
  // Consequence, stated rather than hidden: the HTTP path is a DEV-SERVER capability. `vite
  // build` output has no proxy, so a built bundle pointed at a real node is unauthenticated
  // again. Giving these surfaces a browser-usable credential means adopting the node's
  // web-client session, which is its own piece of work.
  const sessionToken = env.LOCAL_NODE_SESSION_TOKEN

  return {
    plugins: [react()],
    server: {
      port: 5322,
      proxy: origin
        ? {
            '/api/local-node': {
              target: origin,
              changeOrigin: true,
              headers: sessionToken ? { Authorization: `Bearer ${sessionToken}` } : undefined,
            },
          }
        : undefined,
    },
    preview: { port: 5322 },
  }
})
